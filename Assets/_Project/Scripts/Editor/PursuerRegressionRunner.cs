using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Lockdown.Editor
{
    [InitializeOnLoad]
    public static class PursuerRegressionRunner
    {
        private const string Running = "Lockdown.PursuerRegressionRunning";
        private const string ResultFile = "Lockdown.PursuerRegressionResultFile";
        private static readonly TestRunnerApi Runner;
        static PursuerRegressionRunner()
        {
            Runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            Runner.RegisterCallbacks(new Reporter());
        }
        [MenuItem("Tools/Lockdown/Verify Wandering Guard Shooting Regression")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode before running tests.");
            SessionState.SetBool(Running, true);
            SessionState.SetString(ResultFile, "Logs/PursuerWandererRegression.xml");
            Runner.Execute(new ExecutionSettings(new Filter {
                testMode = TestMode.PlayMode,
                testNames = new[] { "Lockdown.Tests.EnemyShootingTests" }
            }));
        }
        [MenuItem("Tools/Lockdown/Verify Pursuer Decision Tests")]
        public static void RunDecisions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode before running tests.");
            SessionState.SetBool(Running, true);
            SessionState.SetString(ResultFile, "Logs/PursuerDecisionTests.xml");
            Runner.Execute(new ExecutionSettings(new Filter {
                testMode = TestMode.EditMode,
                testNames = new[] { "Lockdown.Tests.PursuerDecisionTests" }
            }));
        }
        private sealed class Reporter : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(Running, false)) return;
                SessionState.SetBool(Running, false);
                TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(SessionState.GetString(ResultFile, "Logs/PursuerRegression.xml")));
                Debug.Log("Pursuer-related regression: " + result.ResultState);
            }
        }
    }
}
