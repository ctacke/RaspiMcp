using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Gpio.Tools;

namespace RaspiMcp.Gpio;

/// <summary>Drop-in plugin controlling BCM GPIO pins (including a 3-pin digital RGB LED) via pinctrl over SSH.</summary>
public class GpioPlugin : IMcpPlugin
{
    public string Name => "Gpio";

    public void Register(IServiceCollection services, IConfiguration configuration) { }

    public void RegisterTools(IMcpToolRegistry registry) =>
        registry.Register<GpioTools>();
}
