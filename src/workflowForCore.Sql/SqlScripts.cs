// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
using System.Data.SqlClient;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace WorkflowForCore.Sql;

// .NET Framework 4.8's SQL scripts for WF3's SQL services, as System.Workflow.Runtime carries them, and how to run them:
// as sqlcmd does, one batch at a time between GO lines.
public static class SqlScripts
{
    public static readonly string[] Persistence = ["SqlPersistenceService_Schema.sql", "SqlPersistenceService_Logic.sql"];
    public static readonly string[] Tracking = ["Tracking_Schema.sql", "Tracking_Logic.sql"];

    static Assembly Runtime => typeof(System.Workflow.Runtime.WorkflowRuntime).Assembly;

    // The script's text. The files are UTF-16 with a byte order mark, as .NET Framework ships them.
    public static string Read(string file)
    {
        using Stream stream = Runtime.GetManifestResourceStream("System.Workflow.Runtime.Sql." + file)
            ?? throw new FileNotFoundException("System.Workflow.Runtime has no script " + file, file);
        using var reader = new StreamReader(stream, Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    // The script's exact bytes.
    public static byte[] ReadBytes(string file)
    {
        using Stream stream = Runtime.GetManifestResourceStream("System.Workflow.Runtime.Sql." + file)
            ?? throw new FileNotFoundException("System.Workflow.Runtime has no script " + file, file);
        var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    public static IEnumerable<string> Batches(string script)
    {
        var batch = new StringBuilder();
        foreach (string line in script.Split('\n'))
        {
            if (Regex.IsMatch(line, @"^\s*GO\s*$", RegexOptions.IgnoreCase))
            {
                if (batch.ToString().Trim().Length > 0) yield return batch.ToString();
                batch.Clear();
            }
            else
                batch.Append(line.TrimEnd('\r')).Append('\n');
        }
        if (batch.ToString().Trim().Length > 0) yield return batch.ToString();
    }

    // Runs the scripts in the database the connection string names.
    public static void Run(string connectionString, IEnumerable<string> files, TextWriter? log = null)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        foreach (string file in files)
        {
            int count = 0;
            foreach (string batch in Batches(Read(file)))
            {
                using var command = new SqlCommand(batch, connection) { CommandTimeout = 300 };
                command.ExecuteNonQuery();
                count++;
            }
            log?.WriteLine($"{file}: {count} batches");
        }
    }
}
