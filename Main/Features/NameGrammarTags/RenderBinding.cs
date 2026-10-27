namespace TAOM.Features.NameGrammarTags;

/// <summary>Binds one text variable to the Name, InformalName, FormalName or link of the campaign object with this id.</summary>
public readonly record struct RenderBinding(string Variable, NameKind Kind, NameForm Form, string ObjectId);
