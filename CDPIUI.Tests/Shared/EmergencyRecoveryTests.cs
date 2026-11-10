using CDPIUI.Helper.Basic;

namespace CDPIUI.Tests.Shared;

public class EmergencyRecoveryTests
{
    [Fact]
    public void LoggerInitializationFailureDoesNotPreventFollowingRecovery()
    {
        EmergencyRecovery.BestEffort(() => throw new TypeInitializationException("SettingsManager", new IOException()));
        Assert.Equal("fallback", EmergencyRecovery.Read<string>(() => throw new IOException("Missing version metadata"), "fallback"));
    }

    [Fact]
    public void BrokenExceptionFormattingRetainsItsTypeName()
    {
        var exception = new BrokenException();
        Assert.Equal(typeof(BrokenException).FullName, EmergencyRecovery.Describe(exception));
    }

    [Fact]
    public void BlankExceptionFormattingRetainsItsTypeName()
    {
        Assert.Equal(typeof(BlankException).FullName, EmergencyRecovery.Describe(new BlankException()));
    }

    [Fact]
    public void FatalRuntimeFailureIsNotSuppressed()
    {
        Assert.Throws<OutOfMemoryException>(() => EmergencyRecovery.BestEffort(() => throw new OutOfMemoryException()));
    }

    private sealed class BrokenException : Exception
    {
        public override string ToString() => throw new InvalidOperationException("Broken exception formatter");
    }

    private sealed class BlankException : Exception
    {
        public override string ToString() => "   ";
    }
}
