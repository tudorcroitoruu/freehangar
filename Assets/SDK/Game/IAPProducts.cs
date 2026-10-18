// This file is generated from Assets/sdk_config.json. Do not edit it manually.
public readonly struct IAPProduct
{
    internal string Id { get; }

    private IAPProduct(string id) => Id = id;

    public static readonly IAPProduct noads = new IAPProduct("projectname.noads");

    public override string ToString() => Id;
}
