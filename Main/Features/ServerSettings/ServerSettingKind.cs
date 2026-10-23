namespace TAOM.Features.ServerSettings;

/// <summary>The value shape of one MCM setting, read from its single MCM property attribute and its property type.</summary>
internal enum ServerSettingKind
{
    Bool,
    Float,
    Int,
    Text,
    Dropdown,

    /// <summary>An MCM button (a <c>System.Action</c>): it runs something, so the file cannot set it.</summary>
    Button,

    /// <summary>
    /// A settable property with no MCM attribute: a parked setting whose attribute stack is commented out (the
    /// shader precompilation pair in TaomSettings). MCM neither loads nor saves it, so the file does not either.
    /// </summary>
    Parked,

    /// <summary>No single known attribute, or an attribute on a property type it cannot drive. A schema test fails on any.</summary>
    Unsupported,
}
