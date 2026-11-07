using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace PearHonorific.Core;

/// <summary>Thin wrapper over the Honorific plugin's IPC. Index 0 is the local player.</summary>
public sealed class HonorificIpc
{
    private readonly ICallGateSubscriber<int, string, object> setTitle;
    private readonly ICallGateSubscriber<int, object> clearTitle;

    public HonorificIpc(IDalamudPluginInterface pluginInterface)
    {
        setTitle = pluginInterface.GetIpcSubscriber<int, string, object>("Honorific.SetCharacterTitle");
        clearTitle = pluginInterface.GetIpcSubscriber<int, object>("Honorific.ClearCharacterTitle");
    }

    /// <returns>false if Honorific isn't loaded/ready.</returns>
    public bool Set(string json)
    {
        try { setTitle.InvokeAction(0, json); return true; }
        catch (IpcNotReadyError) { return false; }
    }

    public bool Clear()
    {
        try { clearTitle.InvokeAction(0); return true; }
        catch (IpcNotReadyError) { return false; }
    }
}
