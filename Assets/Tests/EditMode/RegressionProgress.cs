using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly:TestRunCallback(typeof(PCShopSell.Tests.RegressionProgress))]
namespace PCShopSell.Tests
{
    /// <summary>Keep batch regressions diagnosable when a time-advancement case stalls.</summary>
    public sealed class RegressionProgress:ITestRunCallback
    {
        public void RunStarted(ITest tests)
        {
            Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None);
            Debug.Log("REGRESSION START "+tests.TestCaseCount);
        }
        public void RunFinished(ITestResult result)=>Debug.Log("REGRESSION RESULT "+result.ResultState);
        public void TestStarted(ITest test){if(!test.IsSuite)Debug.Log("REGRESSION CASE "+test.FullName);}
        public void TestFinished(ITestResult result)
        {
            if(result.Test.IsSuite)return;
            Debug.Log("REGRESSION DONE "+result.Test.FullName+" "+result.ResultState);
            string output=System.Environment.GetEnvironmentVariable("PCSHOP_QA_RESULTS");
            if(string.IsNullOrEmpty(output))return;
            System.IO.File.AppendAllText(System.IO.Path.Combine(output,"cases.jsonl"),JsonUtility.ToJson(new CaseResult {
                name=result.Test.FullName,result=result.ResultState.ToString(),seconds=result.Duration,message=result.Message,stack=result.StackTrace
            })+"\n");
        }
        [System.Serializable] sealed class CaseResult { public string name,result,message,stack;public double seconds; }
    }
}
