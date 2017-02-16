#r "System.Xml.Linq"
using System.IO;
using System.Xml.Linq;
using System.Text.RegularExpressions;

if (Args.Count != 2)
{
    WriteLine("Usage: ConvertTestOutputToXml.csx <results.txt> <results.xml>");
    return 1;
}

int passed = 0;
int failed = 0;
int skipped = 0;

var lines = File.ReadAllLines(Args[0]);

if (lines.Length == 0)
{
    WriteLine("No results found");
    return 2;
}

string firstLine = lines[0];
if (!firstLine.StartsWith("Test run for "))
{
    WriteLine("Unexpected data");
    return 3;
}

var collectionXml = new XElement("collection");

int i = 1;
while (i < lines.Length)
{
    string line = lines[i];

    XElement failureXml;
    string testName;
    if (line.StartsWith("Passed"))
    {
        testName = line.Substring("Passed".Length).Trim();
        failureXml = null;
        passed++;
        i++;
    }
    else if (line.StartsWith("Skipped"))
    {
        testName = line.Substring("Skipped".Length).Trim();
        failureXml = null;
        skipped++;
        i++;
    }
    else if(line.StartsWith("Failed"))
    {
        testName = line.Substring("Failed".Length).Trim();
        failed++;
        i++;

        var message = new StringBuilder();
        while (i < lines.Length && !lines[i].StartsWith("Passed") && !lines[i].StartsWith("Failed") && !lines[i].StartsWith("Stack Trace:"))
        {
            message.AppendLine(lines[i]);
            i++;
        }

        var stackTrace = new StringBuilder();
        while (i < lines.Length && !lines[i].StartsWith("Passed") && !lines[i].StartsWith("Failed"))
        {
            stackTrace.AppendLine(lines[i]);
            i++;
        }

        failureXml = new XElement("failure");
        var messageXml = new XElement("message");
        messageXml.SetValue(message.ToString());
        var stackTraceXml = new XElement("stack-trace");
        stackTraceXml.SetValue(stackTrace.ToString());
        failureXml.Add(messageXml);
        failureXml.Add(stackTraceXml);

    }
    else
    {
        i++;
        continue;
    }

    var testXml = new XElement("test");
    testXml.SetAttributeValue("name", testName);
    testXml.SetAttributeValue("result", failureXml != null ? "Fail" : "Pass");
    if (failureXml != null)
    {
        testXml.Add(failureXml);
    }

    collectionXml.Add(testXml);
}

collectionXml.SetAttributeValue("total", (passed + failed + skipped).ToString());
collectionXml.SetAttributeValue("passed", passed.ToString());
collectionXml.SetAttributeValue("failed", failed.ToString());
collectionXml.SetAttributeValue("skipped", skipped.ToString());
collectionXml.SetAttributeValue("name", "all tests");

var xml = new XDocument();
var assembliesXml = new XElement("assemblies");
var assemblyXml = new XElement("assembly");

var parts = firstLine.Substring("Test run for ".Length).Split(new[] { '(', ')' });
assemblyXml.SetAttributeValue("name", parts[0]);
assemblyXml.SetAttributeValue("environment", parts[1]);
assemblyXml.Add(collectionXml);

assembliesXml.Add(assemblyXml);
xml.Add(assembliesXml);
xml.Save(Args[1]);

