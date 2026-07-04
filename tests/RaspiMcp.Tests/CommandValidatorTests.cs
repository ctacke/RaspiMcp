using RaspiMcp.Ssh.Services;
using Xunit;

namespace RaspiMcp.Tests;

public class CommandValidatorTests
{
    private readonly CommandValidator _validator = new();

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm -rf /home/user")]
    [InlineData("mkfs.ext4 /dev/sda1")]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("shutdown now")]
    [InlineData("reboot")]
    [InlineData("poweroff")]
    [InlineData("halt")]
    [InlineData("init 0")]
    [InlineData("systemctl poweroff")]
    [InlineData("systemctl reboot")]
    public void Validate_BlockedCommand_ReturnsInvalid(string command)
    {
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.NotNull(result.RejectionReason);
    }

    [Theory]
    [InlineData("ls -la")]
    [InlineData("cat /etc/hostname")]
    [InlineData("systemctl status nginx")]
    [InlineData("journalctl -u myapp -n 100")]
    [InlineData("df -h")]
    [InlineData("ps aux")]
    [InlineData("tail -n 50 /var/log/syslog")]
    public void Validate_SafeCommand_ReturnsValid(string command)
    {
        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public void Validate_EmptyCommand_ReturnsInvalid()
    {
        var result = _validator.Validate("   ");
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("RM -RF /")]
    [InlineData("SHUTDOWN now")]
    [InlineData("REBOOT")]
    public void Validate_BlockedCommand_CaseInsensitive(string command)
    {
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
    }
}
