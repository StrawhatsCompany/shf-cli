using Shf.Cli.Services;

namespace Shf.Cli.Tests.Services;

public class FactoryMutatorTests
{
    private const string EmptyFactory = """
        using Business.Providers;
        using Business.Providers.Sms;

        namespace Providers.Sms;

        internal sealed class ProviderFactory : IProviderFactory<SmsProviderCredential, ISmsProvider>
        {
            public ISmsProvider Create(SmsProviderCredential credential) =>
                credential.ProviderType switch
                {
                    _ => throw new NotSupportedException($"{credential.ProviderType} is not supported from Providers.Sms")
                };
        }
        """;

    [Fact]
    public void Preserves_the_blank_line_between_usings_and_namespace()
    {
        var after = FactoryMutator.AddDriverCase(EmptyFactory, "Sms", "Twilio");

        // After the insert, there must still be at least one empty line between the last using
        // statement and the `namespace` declaration. Walk lines instead of asserting on raw
        // whitespace so the test is robust to LF / CRLF.
        var lines = after.Replace("\r\n", "\n").Split('\n');
        var lastUsing = Array.FindLastIndex(lines, l => l.TrimStart().StartsWith("using ", StringComparison.Ordinal));
        var ns = Array.FindIndex(lines, l => l.TrimStart().StartsWith("namespace ", StringComparison.Ordinal));

        Assert.True(lastUsing >= 0 && ns > lastUsing);
        Assert.True(string.IsNullOrWhiteSpace(lines[lastUsing + 1]),
            $"Expected a blank line between the last `using` and `namespace`, got: '{lines[lastUsing + 1]}'");
    }

    [Fact]
    public void Adds_using_for_the_driver_namespace()
    {
        var after = FactoryMutator.AddDriverCase(EmptyFactory, "Sms", "Twilio");

        Assert.Contains("using Providers.Sms.Twilio;", after);
    }

    [Fact]
    public void Adds_switch_case_before_the_default()
    {
        var after = FactoryMutator.AddDriverCase(EmptyFactory, "Sms", "Twilio");

        var twilioIdx = after.IndexOf("SmsProviderType.Twilio => new TwilioProvider(credential),", StringComparison.Ordinal);
        var defaultIdx = after.IndexOf("_ => throw", StringComparison.Ordinal);

        Assert.True(twilioIdx > 0);
        Assert.True(defaultIdx > 0);
        Assert.True(twilioIdx < defaultIdx);
    }

    [Fact]
    public void Inserts_subsequent_cases_alongside_existing_ones()
    {
        var first = FactoryMutator.AddDriverCase(EmptyFactory, "Sms", "Twilio");

        var second = FactoryMutator.AddDriverCase(first, "Sms", "Vonage");

        Assert.Contains("SmsProviderType.Twilio => new TwilioProvider(credential),", second);
        Assert.Contains("SmsProviderType.Vonage => new VonageProvider(credential),", second);
        Assert.Contains("using Providers.Sms.Vonage;", second);
    }

    [Fact]
    public void Is_idempotent_for_repeated_calls_with_the_same_driver()
    {
        var first = FactoryMutator.AddDriverCase(EmptyFactory, "Sms", "Twilio");
        var second = FactoryMutator.AddDriverCase(first, "Sms", "Twilio");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Throws_when_default_case_is_missing()
    {
        const string malformed = """
            using Business.Providers;

            namespace Providers.Sms;

            internal sealed class ProviderFactory
            {
            }
            """;

        Assert.Throws<InvalidOperationException>(() => FactoryMutator.AddDriverCase(malformed, "Sms", "Twilio"));
    }
}
