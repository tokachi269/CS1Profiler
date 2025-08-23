Write-Host "Creating minimal safe version..." -ForegroundColor Yellow

$minimalCode = @"
using ICities;
using System;
using UnityEngine;

[assembly: System.Reflection.AssemblyTitle("CS1Profiler")]
[assembly: System.Reflection.AssemblyDescription("Cities Skylines performance profiling mod")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace CS1Profiler
{
    public sealed class Mod : ICities.IUserMod
    {
        public string Name { get { return "CS1 Method Profiler (Safe Mode)"; } }
        public string Description { get { return "Basic profiling without CSV - .NET 3.5 compatible"; } }
        
        public void OnEnabled()
        {
            UnityEngine.Debug.Log("[CS1Profiler] Safe Mode OnEnabled - No Path.Combine used!");
        }
        
        public void OnDisabled()
        {
            UnityEngine.Debug.Log("[CS1Profiler] Safe Mode OnDisabled");
        }
        
        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                var group = helper.AddGroup("CS1 Profiler (Safe Mode)");
                group.AddButton("Test Button", () => {
                    float fps = 1.0f / UnityEngine.Time.deltaTime;
                    UnityEngine.Debug.Log("FPS: " + fps.ToString("F1"));
                });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("OnSettingsUI error: " + e.Message);
            }
        }
    }
}
"@

$minimalCode | Out-File -FilePath "src\CS1Profiler.cs" -Encoding UTF8
Write-Host "Created minimal safe version" -ForegroundColor Green
