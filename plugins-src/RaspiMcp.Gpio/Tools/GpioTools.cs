using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RaspiMcp.Core.Interfaces;

namespace RaspiMcp.Gpio.Tools;

/// <summary>MCP tools for controlling BCM GPIO pins over SSH via pinctrl, including a 3-pin digital RGB LED convenience tool.</summary>
[McpServerToolType]
public class GpioTools
{
    private readonly ICommandExecutor _executor;
    private readonly ILogger<GpioTools> _logger;

    public GpioTools(ICommandExecutor executor, ILogger<GpioTools> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    // ---- Shared low-level command builders ----

    private static string BuildOutputCommand(int pin, bool high) =>
        $"pinctrl set {pin} op {(high ? "dh" : "dl")}";

    private static string BuildInputCommand(int pin, string pull) =>
        $"pinctrl set {pin} ip {ParsePull(pull)}";

    private static bool ParseLevel(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "high" => true,
        "low" => false,
        null or "" => throw new ArgumentException("level is required when direction is 'output' (high or low)."),
        _ => throw new ArgumentException($"Unknown level '{level}'. Valid: high, low.")
    };

    private static string ParsePull(string pull) => pull.Trim().ToLowerInvariant() switch
    {
        "up" => "pu",
        "down" => "pd",
        "none" or "" => "pn",
        _ => throw new ArgumentException($"Unknown pull '{pull}'. Valid: up, down, none.")
    };

    // ---- RGB LED convenience tool ----

    private static readonly Dictionary<string, (bool R, bool G, bool B)> ColorMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["off"] = (false, false, false),
            ["black"] = (false, false, false),
            ["red"] = (true, false, false),
            ["green"] = (false, true, false),
            ["blue"] = (false, false, true),
            ["yellow"] = (true, true, false),
            ["magenta"] = (true, false, true),
            ["cyan"] = (false, true, true),
            ["white"] = (true, true, true),
        };

    public static (bool Red, bool Green, bool Blue) ResolveColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            throw new ArgumentException("Color name must not be empty.");

        if (ColorMap.TryGetValue(color.Trim(), out var state))
            return state;

        throw new ArgumentException(
            $"Unknown color '{color}'. Valid colors: {string.Join(", ", ColorMap.Keys)}.");
    }

    public static string BuildSetCommand(int redPin, int greenPin, int bluePin, (bool Red, bool Green, bool Blue) state) =>
        string.Join(" && ",
            BuildOutputCommand(redPin, state.Red),
            BuildOutputCommand(greenPin, state.Green),
            BuildOutputCommand(bluePin, state.Blue));

    [McpServerTool, Description(
        "Sets a 3-pin digital RGB LED (no PWM/brightness - 8 combinatorial colors) by driving three " +
        "BCM GPIO pins high/low over SSH using pinctrl. Valid colors: off, red, green, blue, yellow, " +
        "magenta, cyan, white.")]
    public async Task<string> set_rgb_led(
        [Description("BCM GPIO pin number wired to the LED's red channel.")] int redPin,
        [Description("BCM GPIO pin number wired to the LED's green channel.")] int greenPin,
        [Description("BCM GPIO pin number wired to the LED's blue channel.")] int bluePin,
        [Description("Color name: off, red, green, blue, yellow, magenta, cyan, or white.")] string color,
        CancellationToken ct = default)
    {
        try
        {
            var state = ResolveColor(color);

            if (redPin == greenPin || redPin == bluePin || greenPin == bluePin)
                throw new ArgumentException(
                    $"redPin ({redPin}), greenPin ({greenPin}), and bluePin ({bluePin}) must all be distinct BCM pins.");

            var command = BuildSetCommand(redPin, greenPin, bluePin, state);
            var result = await _executor.ExecuteAsync(command, ct);

            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"pinctrl command failed (exit {result.ExitCode}): {result.Stderr.Trim()}");

            return JsonSerializer.Serialize(new
            {
                color = color.Trim().ToLowerInvariant(),
                pins = new { red = redPin, green = greenPin, blue = bluePin },
                state = new { red = state.Red, green = state.Green, blue = state.Blue },
                command,
                exitCode = result.ExitCode
            });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    // ---- Generic single-pin tools ----

    [McpServerTool, Description(
        "Configures a single BCM GPIO pin as input or output over SSH via pinctrl. For output, drives " +
        "it high or low. For input, optionally enables an internal pull-up or pull-down resistor so a " +
        "floating input doesn't need an external resistor wired in (default: none/floating).")]
    public async Task<string> set_gpio_pin(
        [Description("BCM GPIO pin number.")] int pin,
        [Description("\"input\" or \"output\".")] string direction,
        [Description("Required when direction is \"output\": \"high\" or \"low\".")] string? level = null,
        [Description("Only used when direction is \"input\": \"up\", \"down\", or \"none\" (default \"none\").")] string pull = "none",
        CancellationToken ct = default)
    {
        try
        {
            string command = direction.Trim().ToLowerInvariant() switch
            {
                "output" => BuildOutputCommand(pin, ParseLevel(level)),
                "input" => BuildInputCommand(pin, pull),
                _ => throw new ArgumentException($"Unknown direction '{direction}'. Valid: input, output.")
            };

            var result = await _executor.ExecuteAsync(command, ct);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"pinctrl command failed (exit {result.ExitCode}): {result.Stderr.Trim()}");

            return JsonSerializer.Serialize(new
            {
                pin,
                direction = direction.Trim().ToLowerInvariant(),
                level,
                pull,
                command,
                exitCode = result.ExitCode
            });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [McpServerTool, Description(
        "Reads the current mode (input/output), pull configuration, and level of a BCM GPIO pin via " +
        "`pinctrl get` over SSH.")]
    public async Task<string> get_gpio_pin(
        [Description("BCM GPIO pin number.")] int pin,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _executor.ExecuteAsync($"pinctrl get {pin}", ct);
            return JsonSerializer.Serialize(new
            {
                pin,
                stdout = result.Stdout.Trim(),
                stderr = result.Stderr,
                exitCode = result.ExitCode
            });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>Same error-shape contract as SshTools.HandleError - a local copy since the two live in separate assemblies.</summary>
    private string HandleError(Exception ex)
    {
        _logger.LogWarning(ex, "Tool call failed");
        return JsonSerializer.Serialize(new
        {
            error = ex.Message,
            errorType = ex.GetType().Name,
            rejected = true
        });
    }
}
