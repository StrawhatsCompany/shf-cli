using Shf.Cli.Services;

namespace Shf.Cli.Tests.Services;

public class EnumMutatorTests
{
    [Fact]
    public void Adds_member_to_empty_enum_with_zero_value()
    {
        const string before = """
            namespace Foo;

            public enum FooType
            {
            }
            """;

        var after = EnumMutator.AddMember(before, "Bar");

        Assert.Contains("Bar = 0,", after);
    }

    [Fact]
    public void Adds_member_with_next_int_when_others_exist()
    {
        const string before = """
            namespace Foo;

            public enum FooType
            {
                A = 0,
                B = 1,
                C = 2,
            }
            """;

        var after = EnumMutator.AddMember(before, "D");

        Assert.Contains("D = 3,", after);
    }

    [Fact]
    public void Adds_trailing_comma_to_previous_member_when_missing()
    {
        const string before = """
            namespace Foo;

            public enum FooType
            {
                A = 0
            }
            """;

        var after = EnumMutator.AddMember(before, "B");

        Assert.Contains("A = 0,", after);
        Assert.Contains("B = 1,", after);
    }

    [Fact]
    public void Is_idempotent_when_member_already_present()
    {
        const string before = """
            namespace Foo;

            public enum FooType
            {
                A = 0,
                B = 1,
            }
            """;

        var after = EnumMutator.AddMember(before, "A");

        Assert.Equal(before, after);
    }

    [Fact]
    public void Throws_when_closing_brace_is_missing()
    {
        const string before = "public enum Foo {";

        Assert.Throws<InvalidOperationException>(() => EnumMutator.AddMember(before, "Bar"));
    }
}
