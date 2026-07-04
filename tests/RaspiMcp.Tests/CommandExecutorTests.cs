using Moq;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Core.Models;
using RaspiMcp.Ssh.Services;
using Xunit;

namespace RaspiMcp.Tests;

public class CommandExecutorTests
{
    private static CommandResult OkResult() =>
        new("output", string.Empty, 0, TimeSpan.FromMilliseconds(100));

    [Fact]
    public async Task ExecuteAsync_RejectedCommand_ThrowsWithoutCallingSsh()
    {
        var ssh = new Mock<ISshService>();
        var validator = new Mock<ICommandValidator>();
        var audit = new Mock<IAuditLogger>();
        var hostMgr = new Mock<IHostManager>();

        validator.Setup(v => v.Validate(It.IsAny<string>()))
            .Returns(new ValidationResult(false, "Rejected: dangerous command"));

        var executor = new CommandExecutor(ssh.Object, validator.Object, audit.Object, hostMgr.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteAsync("rm -rf /"));

        ssh.Verify(s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_LogsAuditEntry()
    {
        var ssh = new Mock<ISshService>();
        var validator = new Mock<ICommandValidator>();
        var audit = new Mock<IAuditLogger>();
        var hostMgr = new Mock<IHostManager>();

        validator.Setup(v => v.Validate(It.IsAny<string>()))
            .Returns(new ValidationResult(true));
        ssh.Setup(s => s.ExecuteAsync("ls", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult());
        hostMgr.Setup(h => h.GetCurrentHost())
            .Returns(new HostInfo("test-host", "10.0.0.1", "user"));

        var executor = new CommandExecutor(ssh.Object, validator.Object, audit.Object, hostMgr.Object);

        await executor.ExecuteAsync("ls");

        audit.Verify(a => a.LogCommand(It.Is<AuditEntry>(e =>
            e.Command == "ls" && e.HostAlias == "test-host" && e.ExitCode == 0)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsResultFromSsh()
    {
        var ssh = new Mock<ISshService>();
        var validator = new Mock<ICommandValidator>();
        var audit = new Mock<IAuditLogger>();
        var hostMgr = new Mock<IHostManager>();

        validator.Setup(v => v.Validate(It.IsAny<string>())).Returns(new ValidationResult(true));
        ssh.Setup(s => s.ExecuteAsync("echo hi", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult("hi\n", string.Empty, 0, TimeSpan.FromMilliseconds(50)));
        hostMgr.Setup(h => h.GetCurrentHost()).Returns(new HostInfo("host", "1.2.3.4", "user"));

        var executor = new CommandExecutor(ssh.Object, validator.Object, audit.Object, hostMgr.Object);
        var result = await executor.ExecuteAsync("echo hi");

        Assert.Equal("hi\n", result.Stdout);
        Assert.Equal(0, result.ExitCode);
    }
}
