using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpUnity.Unity;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;
using Newtonsoft.Json.Linq;

namespace McpUnity.Services
{
    /// <summary>
    /// Service for accessing Unity Test Runner functionality
    /// </summary>
    public class TestRunnerService : ITestRunnerService
    {
        private readonly TestRunnerApi _testRunnerApi;
        
        /// <summary>
        /// Get the TestRunnerApi instance
        /// </summary>
        public TestRunnerApi TestRunnerApi => _testRunnerApi;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public TestRunnerService()
        {
            _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        }
        
        /// <summary>
        /// Get a list of available tests, optionally filtered by test mode
        /// </summary>
        /// <param name="testMode">Optional test mode filter (EditMode, PlayMode, or empty for all)</param>
        /// <returns>List of test items matching the specified test mode, or all tests if no mode specified</returns>
        public List<TestItemInfo> GetAllTests(string testMode = "")
        {
            var tests = new List<TestItemInfo>();
            
            // Check if we need to retrieve EditMode tests
            if (string.IsNullOrEmpty(testMode) || testMode.Equals("EditMode", StringComparison.OrdinalIgnoreCase))
            {
                _testRunnerApi.RetrieveTestList(TestMode.EditMode, adaptor => CollectTestItems(adaptor, tests));
            }
            
            // Check if we need to retrieve PlayMode tests
            if (string.IsNullOrEmpty(testMode) || testMode.Equals("PlayMode", StringComparison.OrdinalIgnoreCase))
            {
                _testRunnerApi.RetrieveTestList(TestMode.PlayMode, adaptor => CollectTestItems(adaptor, tests));
            }
            
            return tests;
        }

        /// <summary>
        /// Execute tests with the provided parameters
        /// </summary>
        /// <param name="testMode">Test mode to run</param>
        /// <param name="testFilter">Optional test filter</param>
        /// <param name="completionSource">TaskCompletionSource to resolve when tests are complete</param>
        /// <returns>Task that resolves with test results when tests are complete</returns>
        public async void ExecuteTests(
            TestMode testMode, 
            string testFilter, 
            TaskCompletionSource<JObject> completionSource)
        {
            // Create filter
            var filter = new Filter
            {
                testMode = testMode
            };
                
            // Apply name filter if provided
            if (!string.IsNullOrEmpty(testFilter))
            {
                filter.testNames = new[] { testFilter };
            }
                
            // Execute tests
            _testRunnerApi.Execute(new ExecutionSettings(filter));

            // Use timeout from settings if not specified
            var timeoutSeconds =  McpUnitySettings.Instance.RequestTimeoutSeconds;
            
            Task completedTask = await Task.WhenAny(
                completionSource.Task,
                Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))
            );

            if (completedTask != completionSource.Task)
            {
                completionSource.SetResult(McpUnitySocketHandler.CreateErrorResponse(
                    $"Test run timed out after {timeoutSeconds} seconds",
                    "test_runner_timeout"
                ));
            }
        }
        
        /// <summary>
        /// Recursively collect test items from test adaptors
        /// </summary>
        private void CollectTestItems(ITestAdaptor testAdaptor, List<TestItemInfo> tests, string parentPath = "")
        {
            if (testAdaptor.IsSuite)
            {
                // For suites (namespaces, classes), collect all children
                foreach (var child in testAdaptor.Children)
                {
                    string currentPath = string.IsNullOrEmpty(parentPath) ? testAdaptor.Name : $"{parentPath}.{testAdaptor.Name}";
                    CollectTestItems(child, tests, currentPath);
                }
            }
            else
            {
                // For individual tests, add to the list
                string fullPath = string.IsNullOrEmpty(parentPath) ? testAdaptor.Name : $"{parentPath}.{testAdaptor.Name}";
                
                tests.Add(new TestItemInfo
                {
                    Name = testAdaptor.Name,
                    FullName = testAdaptor.FullName,
                    Path = fullPath,
                    TestMode = testAdaptor.TestMode.ToString(),
                    RunState = testAdaptor.RunState.ToString()
                });
            }
        }
    }
    
    /// <summary>
    /// Information about a test item
    /// </summary>
    public class TestItemInfo
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Path { get; set; }
        public string TestMode { get; set; }
        public string RunState { get; set; }
    }
}
