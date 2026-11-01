using UnityEditor; public static class RefreshMenuScripts { public static object Main() { AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); return "Refresh requested"; } }
