using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

public enum IAPPurchaseStatus { Success, NotInitialized, ProductUnavailable, Cancelled, Failed }

public readonly struct IAPPurchaseResult
{
    public readonly IAPPurchaseStatus Status;
    public readonly string ProductId;
    public readonly string TransactionId;
    public readonly string Currency;
    public readonly decimal LocalizedPrice;
    public bool Succeeded => Status == IAPPurchaseStatus.Success;

    public IAPPurchaseResult(IAPPurchaseStatus status, string productId, string transactionId = null, string currency = null, decimal price = 0)
    { Status = status; ProductId = productId; TransactionId = transactionId; Currency = currency; LocalizedPrice = price; }
}

internal sealed class SDKIAP
{
    private const int MaxInitializationAttempts = 3;
    private readonly SDKConfig config;
    private readonly SDKAnalytics analytics;
    private readonly SDKPrivacyAttribution attribution;
    private readonly HashSet<string> ownedProductIds = new(StringComparer.Ordinal);
    private StoreController store;
    private string pendingId;
    private Action<IAPPurchaseResult> pendingCallback;
    private bool initializationInProgress;
    private bool productsFetched;
    private int productFetchAttempts;
    private bool hasActiveSubscription;
    private bool restoreRequested;
    private bool restoreInProgress;
    private Action<bool> pendingRestoreCallback;
    private readonly List<Action<bool>> pendingSubscriptionCallbacks = new();
    private bool purchaseHistoryFetched;
    private bool purchaseHistoryFetchInProgress;

    public bool IsInitialized => store != null && productsFetched;
    public bool HasActiveSubscription => hasActiveSubscription;

    public SDKIAP(SDKConfig sdkConfig, SDKAnalytics sdkAnalytics, SDKPrivacyAttribution sdkAttribution)
    {
        config = sdkConfig;
        analytics = sdkAnalytics;
        attribution = sdkAttribution;
        EnsureInitialized();
    }

    public void Purchase(string productId, Action<IAPPurchaseResult> callback)
    {
        IAPProductConfig productConfig = FindConfig(productId);
        if (string.IsNullOrWhiteSpace(productId) || productConfig == null)
        {
            callback?.Invoke(new IAPPurchaseResult(IAPPurchaseStatus.ProductUnavailable, productId));
            return;
        }
        if (!string.IsNullOrEmpty(pendingId))
        {
            callback?.Invoke(new IAPPurchaseResult(IAPPurchaseStatus.Failed, productId));
            return;
        }

        pendingId = RuntimeProductId(productConfig);
        pendingCallback = callback;
        if (!IsInitialized)
        {
            Debug.Log("[SDK] IAP purchase is waiting for initialization: " + productId);
            EnsureInitialized();
            return;
        }
        BeginStorePurchase();
    }

    public string LocalizedPrice(string productId)
        => store?.GetProductById(ResolveProductId(productId))?.metadata?.localizedPriceString ?? string.Empty;

    public bool HasReceipt(string productId) => ownedProductIds.Contains(ResolveProductId(productId));

    public void CheckSubscription(Action<bool> callback)
    {
        if (callback == null) return;
        if (purchaseHistoryFetched)
        {
            callback.Invoke(hasActiveSubscription);
            return;
        }

        pendingSubscriptionCallbacks.Add(callback);
        EnsureInitialized();
        if (IsInitialized) RequestPurchaseHistory();
    }

    public void Restore(Action<bool> callback)
    {
        Debug.Log("[SDK IAP] Restore requested.");
        if (restoreRequested || restoreInProgress)
        {
            Debug.LogWarning("[SDK IAP] Restore is already in progress.");
            callback?.Invoke(false);
            return;
        }

        restoreRequested = true;
        pendingRestoreCallback = callback;
        if (!IsInitialized)
        {
            Debug.Log("[SDK IAP] Restore is waiting for store initialization.");
            EnsureInitialized();
            return;
        }

        BeginRestore();
    }

    private async void EnsureInitialized()
    {
        if (IsInitialized || initializationInProgress) return;
        initializationInProgress = true;
        CreateStoreIfNeeded();

        for (int attempt = 1; attempt <= MaxInitializationAttempts; attempt++)
        {
            try
            {
                await store.Connect();
                productFetchAttempts = 1;
                store.FetchProducts(ProductDefinitions());
                return;
            }
            catch (Exception exception)
            {
                Debug.LogError("[SDK] IAP connection failed (attempt " + attempt + "/" + MaxInitializationAttempts + "): " + exception.Message);
                if (attempt < MaxInitializationAttempts) await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));
            }
        }

        initializationInProgress = false;
        CompletePending(IAPPurchaseStatus.NotInitialized);
        CompleteRestore(false, "Store connection failed after all retry attempts.");
    }

    private void CreateStoreIfNeeded()
    {
        if (store != null) return;
        store = UnityIAPServices.StoreController();
        store.ProcessPendingOrdersOnPurchasesFetched(true);
        store.OnProductsFetched += OnProductsFetched;
        store.OnProductsFetchFailed += OnProductsFetchFailed;
        store.OnPurchasePending += OnPurchasePending;
        store.OnPurchaseFailed += OnPurchaseFailed;
        store.OnPurchaseConfirmed += OnPurchaseConfirmed;
        store.OnPurchasesFetched += OnPurchasesFetched;
        store.OnPurchasesFetchFailed += failure =>
        {
            purchaseHistoryFetchInProgress = false;
            Debug.LogError("[SDK IAP] Purchase history fetch failed: " + failure);
            CompleteSubscriptionChecks(false);
            if (restoreInProgress) CompleteRestore(false, "Purchase history could not be fetched.");
        };
        store.OnStoreDisconnected += description => Debug.LogWarning("[SDK] IAP store disconnected: " + description);
    }

    private List<ProductDefinition> ProductDefinitions()
        => config.iap.products.Select(item => new ProductDefinition(RuntimeProductId(item), ParseType(item.type))).ToList();

    private void OnProductsFetched(List<Product> products)
    {
        productsFetched = true;
        initializationInProgress = false;
        Debug.Log("[SDK] IAP initialized.");
        RequestPurchaseHistory();
        BeginRestore();
        BeginStorePurchase();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        Debug.LogError("[SDK] IAP product fetch failed (attempt " + productFetchAttempts + "/" + MaxInitializationAttempts + "): " + failure.FailureReason);
        if (productFetchAttempts >= MaxInitializationAttempts)
        {
            initializationInProgress = false;
            CompletePending(IAPPurchaseStatus.NotInitialized);
            CompleteRestore(false, "Products could not be loaded after all retry attempts.");
            return;
        }
        attribution.StartCoroutine(RetryProductFetch());
    }

    private IEnumerator RetryProductFetch()
    {
        yield return new WaitForSecondsRealtime(Mathf.Pow(2, productFetchAttempts - 1));
        productFetchAttempts++;
        store.FetchProducts(ProductDefinitions());
    }

    private void BeginStorePurchase()
    {
        if (string.IsNullOrEmpty(pendingId)) return;
        var product = store.GetProductById(pendingId);
        if (product == null || !product.availableToPurchase)
        {
            CompletePending(IAPPurchaseStatus.ProductUnavailable);
            return;
        }

        analytics.Design("shop:purchase_start:" + product.definition.id);
        TrackTenjinInitiateCheckout(product);
        attribution.TrackMetaInitiatedCheckout(
            product.definition.id,
            product.metadata?.isoCurrencyCode,
            product.metadata?.localizedPrice ?? 0m,
            FindConfig(product.definition.id)?.usdCents ?? 0);
        try { store.PurchaseProduct(product); }
        catch (Exception exception)
        {
            Debug.LogError("[SDK] Unable to start purchase: " + exception);
            CompletePending(IAPPurchaseStatus.Failed);
        }
    }

    private void TrackTenjinInitiateCheckout(Product product)
    {
        var instance = attribution.Tenjin;
        if (instance == null) return;

        int usdCents = FindConfig(product.definition.id)?.usdCents ?? 0;
        string priceSource = "configured catalog";
        var metadata = product.metadata;
        if (metadata != null &&
            string.Equals(metadata.isoCurrencyCode, "USD", StringComparison.OrdinalIgnoreCase) &&
            metadata.localizedPrice > 0m)
        {
            decimal storeUsdCents = decimal.Round(
                metadata.localizedPrice * 100m,
                0,
                MidpointRounding.AwayFromZero);
            if (storeUsdCents <= int.MaxValue)
            {
                usdCents = (int)storeUsdCents;
                priceSource = "store metadata";
            }
        }

        if (usdCents <= 0)
        {
            Debug.LogWarning(
                "[Tenjin] USD price is not configured for " + product.definition.id +
                "; sending initiateCheckout without a value.");
            instance.SendEvent("initiateCheckout");
            return;
        }

        string eventValue = usdCents.ToString(CultureInfo.InvariantCulture);
        instance.SendEvent("initiateCheckout", eventValue);
        Debug.Log(
            "[Tenjin] initiateCheckout: product=" + product.definition.id +
            ", usd_cents=" + eventValue +
            ", source=" + priceSource + ".");
    }

    private void OnPurchasePending(PendingOrder order)
    {
        var product = ProductFrom(order);
        if (product == null) return;
        if (!ValidateReceipt(order.Info.Receipt))
        {
            if (string.Equals(pendingId, product.definition.id, StringComparison.Ordinal)) CompletePending(IAPPurchaseStatus.Failed);
            return;
        }

        string transaction = !string.IsNullOrEmpty(order.Info.TransactionID) ? order.Info.TransactionID : order.Info.Receipt;
        if (string.IsNullOrEmpty(transaction))
        {
            if (string.Equals(pendingId, product.definition.id, StringComparison.Ordinal)) CompletePending(IAPPurchaseStatus.Failed);
            return;
        }

        bool firstProcessing = !SaveSystem.State.processedTransactions.Contains(transaction);
        if (!firstProcessing)
        {
            ownedProductIds.Add(product.definition.id);
            ApplyStandardEntitlement(product.definition.id);
            UpdateSubscriptionState(order);
            store.ConfirmPurchase(order);
            return;
        }

        var result = ResultFrom(product, order);
        bool standardEntitlementApplied = ApplyStandardEntitlement(product.definition.id);
        bool callbackSucceeded = true;
        bool belongsToPendingRequest = string.Equals(pendingId, product.definition.id, StringComparison.Ordinal);
        if (belongsToPendingRequest)
            callbackSucceeded = CompletePending(result);
        else
            callbackSucceeded = SDKManager.NotifyPurchase(result) || standardEntitlementApplied;
        if (!callbackSucceeded)
        {
            Debug.LogError("[SDK] Purchase entitlement was not handled. The order remains unconfirmed and will be delivered again.");
            return;
        }

        ownedProductIds.Add(product.definition.id);
        UpdateSubscriptionState(order);
        SaveSystem.State.processedTransactions.Add(transaction);
        SaveSystem.Save();
        if (!IsSandbox(order.Info.Receipt)) TrackSuccessfulPurchase(product, order);

        // Game-specific entitlement is granted by the success callback/event before store confirmation.
        store.ConfirmPurchase(order);
    }

    private void TrackSuccessfulPurchase(Product product, PendingOrder order)
    {
        double localizedPrice = decimal.ToDouble(product.metadata.localizedPrice);
        int usdCents = FindConfig(product.definition.id)?.usdCents ?? 0;
        SaveSystem.State.totalIapSpendUsd += usdCents / 100.0;
        SaveSystem.Save();
        analytics.Purchase(product.metadata.isoCurrencyCode, (int)Math.Round(localizedPrice * 100), product.definition.id, order.Info.Receipt);
        analytics.TrackFirstPurchase();
        SendTenjinTransaction(product, order, localizedPrice);
        attribution.TrackMetaPurchase(
            product.definition.id,
            product.metadata.isoCurrencyCode,
            product.metadata.localizedPrice,
            usdCents);
        UpdateTenjinConversionValue();
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        var product = ProductFrom(order);
        if (product == null || !string.Equals(pendingId, product.definition.id, StringComparison.Ordinal)) return;
        CompletePending(order.FailureReason == PurchaseFailureReason.UserCancelled ? IAPPurchaseStatus.Cancelled : IAPPurchaseStatus.Failed);
    }

    private void OnPurchaseConfirmed(Order order)
    {
        if (order is FailedOrder failed)
        {
            var product = ProductFrom(order);
            Debug.LogError("[SDK] Purchase confirmation failed for " + product?.definition.id + ": " + failed.Details);
        }
    }

    private void OnPurchasesFetched(Orders orders)
    {
        purchaseHistoryFetchInProgress = false;
        purchaseHistoryFetched = true;
        hasActiveSubscription = false;
        int ownedProducts = 0;
        foreach (var order in orders.ConfirmedOrders)
        {
            var product = ProductFrom(order);
            if (product != null)
            {
                ownedProductIds.Add(product.definition.id);
                ownedProducts++;
                ApplyStandardEntitlement(product.definition.id);
            }
            UpdateSubscriptionState(order);
        }
        foreach (var order in orders.PendingOrders) UpdateSubscriptionState(order);
        Debug.Log("[SDK IAP] Purchase history fetched. Owned products: " + ownedProducts + ".");
        CompleteSubscriptionChecks(hasActiveSubscription);
    }

    private void RequestPurchaseHistory()
    {
        if (!IsInitialized || purchaseHistoryFetchInProgress) return;
        purchaseHistoryFetchInProgress = true;
        store.FetchPurchases();
    }

    private void CompleteSubscriptionChecks(bool subscribed)
    {
        if (pendingSubscriptionCallbacks.Count == 0) return;
        var callbacks = pendingSubscriptionCallbacks.ToArray();
        pendingSubscriptionCallbacks.Clear();
        foreach (Action<bool> callback in callbacks)
        {
            try { callback.Invoke(subscribed); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private void BeginRestore()
    {
        if (!restoreRequested || restoreInProgress || !IsInitialized) return;
        restoreRequested = false;
        restoreInProgress = true;
        Debug.Log("[SDK IAP] Starting App Store restore transactions.");
        try
        {
            store.RestoreTransactions((success, error) => CompleteRestore(success, error));
        }
        catch (Exception exception)
        {
            CompleteRestore(false, exception.Message);
        }
    }

    private void CompleteRestore(bool success, string error = null)
    {
        if (!restoreRequested && !restoreInProgress && pendingRestoreCallback == null) return;
        restoreRequested = false;
        restoreInProgress = false;
        if (success)
            Debug.Log("[SDK IAP] App Store restore succeeded. Purchase history refresh was requested.");
        else
            Debug.LogError("[SDK IAP] Restore failed: " + (string.IsNullOrWhiteSpace(error) ? "Unknown App Store error." : error));
        var callback = pendingRestoreCallback;
        pendingRestoreCallback = null;
        callback?.Invoke(success);
    }

    private bool ApplyStandardEntitlement(string productId)
    {
        IAPProductConfig noAds = Array.Find(
            config.iap.products,
            item => string.Equals(item.scope, "noads", StringComparison.Ordinal));
        if (noAds == null || !string.Equals(productId, RuntimeProductId(noAds), StringComparison.Ordinal)) return false;
        SDKManager.DisableAds();
        Debug.Log("[SDK IAP] Standard entitlement applied: " + productId + ".");
        return true;
    }

    private static Product ProductFrom(Order order) => order?.CartOrdered?.Items()?.FirstOrDefault()?.Product;

    private static IAPPurchaseResult ResultFrom(Product product, Order order)
        => new(IAPPurchaseStatus.Success, product.definition.id, order.Info.TransactionID,
            product.metadata.isoCurrencyCode, product.metadata.localizedPrice);

    private static bool ValidateReceipt(string receipt)
    {
#if UNITY_EDITOR
        return true;
#else
        try
        {
            byte[] googleKey = null;
            byte[] appleKey = null;
#if UNITY_ANDROID
            googleKey = ReadTangle("UnityEngine.Purchasing.Security.GooglePlayTangle");
#elif UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX
            appleKey = ReadTangle("UnityEngine.Purchasing.Security.AppleTangle");
#endif
            var validator = new UnityEngine.Purchasing.Security.CrossPlatformValidator(googleKey, appleKey, Application.identifier);
            validator.Validate(receipt);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[SDK] Receipt validation failed: " + exception.Message);
            return false;
        }
#endif
    }

    private static byte[] ReadTangle(string fullTypeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var method = assembly.GetType(fullTypeName)?.GetMethod("Data", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method != null) return method.Invoke(null, null) as byte[];
        }
        throw new InvalidOperationException(fullTypeName + " is missing. Generate receipt validation obfuscation data from Unity IAP.");
    }

    private void SendTenjinTransaction(Product product, Order order, double unitPrice)
    {
        var instance = attribution.Tenjin;
        if (instance == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var wrapper = UnityEngine.Purchasing.MiniJSON.Json.Deserialize(order.Info.Receipt) as Dictionary<string, object>;
            var payload = UnityEngine.Purchasing.MiniJSON.Json.Deserialize(wrapper?["Payload"] as string) as Dictionary<string, object>;
            instance.Transaction(product.definition.id, product.metadata.isoCurrencyCode, 1, unitPrice, null,
                payload?["json"] as string, payload?["signature"] as string);
        }
        catch (Exception exception) { Debug.LogWarning("[SDK] Tenjin Android receipt parse failed: " + exception.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
        instance.Transaction(product.definition.id, product.metadata.isoCurrencyCode, 1, unitPrice,
            order.Info.TransactionID, order.Info.Receipt, null);
#endif
    }

    private void UpdateSubscriptionState(Order order)
    {
        foreach (var info in order.Info.PurchasedProductInfo)
            if (info?.subscriptionInfo?.IsSubscribed() == Result.True) hasActiveSubscription = true;
    }

    private void UpdateTenjinConversionValue()
    {
        var instance = attribution.Tenjin;
        if (instance == null) return;
        int conversionValue = Mathf.RoundToInt((float)SaveSystem.State.totalIapSpendUsd) + 4;
        if (hasActiveSubscription && conversionValue < 55) conversionValue = 55;
        conversionValue = Mathf.Min(63, conversionValue);
        if (conversionValue < 5) return;
        if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android)
            instance.UpdateConversionValue(conversionValue);
        Debug.Log("[SDK] Tenjin conversion value: " + conversionValue);
    }

    private static bool IsSandbox(string receipt)
    {
#if UNITY_EDITOR
        return true;
#elif UNITY_IOS
        return SDKIsSandboxReceipt();
#elif UNITY_ANDROID
        if (Debug.isDebugBuild) return true;
        return !string.IsNullOrEmpty(receipt) &&
               System.Text.RegularExpressions.Regex.IsMatch(receipt, "\\\"purchaseType\\\"\\s*:\\s*0");
#else
        return Debug.isDebugBuild;
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern bool SDKIsSandboxReceipt();
#endif

    private bool CompletePending(IAPPurchaseStatus status) => CompletePending(new IAPPurchaseResult(status, pendingId));

    private bool CompletePending(IAPPurchaseResult result)
    {
        var callback = pendingCallback;
        pendingId = null;
        pendingCallback = null;
        try { callback?.Invoke(result); return true; }
        catch (Exception exception) { Debug.LogException(exception); return false; }
    }

    private string ResolveProductId(string id)
    {
        IAPProductConfig product = FindConfig(id);
        return product == null ? id : RuntimeProductId(product);
    }

    private IAPProductConfig FindConfig(string id)
        => Array.Find(
            config.iap.products,
            item => string.Equals(item.id, id, StringComparison.Ordinal) ||
                    string.Equals(RuntimeProductId(item), id, StringComparison.Ordinal));

    private static string RuntimeProductId(IAPProductConfig product)
    {
        bool isTestBundle = string.Equals(
            Application.identifier,
            "com.atose.test",
            StringComparison.Ordinal);
        return isTestBundle && string.Equals(product.scope, "noads", StringComparison.Ordinal)
            ? "removeads"
            : product.id;
    }

    private static ProductType ParseType(string value) => value switch
    { "NonConsumable" => ProductType.NonConsumable, "Subscription" => ProductType.Subscription, _ => ProductType.Consumable };
}
