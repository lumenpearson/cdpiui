using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.Views.CreateConfigHelper;

public class VariableModel
{
    public string Name { get; set; }
    public string Value { get; set; }
    public AvailableVarValues AvailableValues { get; set; } = null;
}
