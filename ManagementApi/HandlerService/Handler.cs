using System.Collections;
using ManagementApi.Handler.Plugins;
using ManagementApi.Queue.Wrapper;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Handler;


public class Handler(ILogger logger) {
    ArrayList Plugins = new();
    bool Stop = false;

    public void AddPlugin(PluginBase plugin) {
        Plugins.Add(plugin);
        logger.LogInformation($"Registered plugin for queue {plugin.Queue}.");
    }

    public async Task RunLoop() {
        logger.LogInformation("Starting to receive messages.");
        while (!Stop) {
            foreach (PluginBase plugin in Plugins) {
                try {
                    logger.LogDebug($"try plugin: {plugin.Queue}");
                    await plugin.HandleOneMessage();
                } catch (Exception ex) {
                    logger.LogError($"Error handling message in plugin: {plugin.Queue}: {ex.Message}");
                }
            }
            Thread.Sleep(100);
        }
        logger.LogInformation("Stopping to receive messages.");
    }
}