namespace TAOM.Features.FireAndTribute;

public interface IFireAndTributeConfigProvider
{
    /// <summary>The validated config, loaded once per process; an edit to the file needs a full restart.</summary>
    FireAndTributeConfig GetConfig();
}
