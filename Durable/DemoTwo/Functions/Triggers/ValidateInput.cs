using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DemoTwo.Functions.Triggers;
public static class ValidateInput
{
    private const string _validationPattern = @"^[A-Za-z\s]+$";
    private static readonly Regex _validatorRegex = new(_validationPattern, RegexOptions.Compiled);

    [Function("func-validate-input")]
    public static string Run([ActivityTrigger] string input, FunctionContext context)
    {
        var logger = context.GetLogger("func-validate-input");
        logger.LogInformation("Validating input: {Input}", input);

        var isValid = _validatorRegex.IsMatch(input);

        return isValid ? input.Trim() : string.Empty;
    }
}