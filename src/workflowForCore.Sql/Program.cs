// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
using WorkflowForCore.Sql;

const string usage = """
    workflowforcore-sql: the SQL Server databases of WF3's SqlWorkflowPersistenceService and SqlTrackingService, with
    .NET Framework 4.8's scripts (SqlPersistenceService_Schema.sql, SqlPersistenceService_Logic.sql, Tracking_Schema.sql,
    Tracking_Logic.sql), as running them with sqlcmd on .NET Framework does.

      workflowforcore-sql install persistence|tracking|all "<connection string>"
          Runs the scripts in the (existing) database the connection string names.
      workflowforcore-sql export persistence|tracking|all <folder>
          Writes the scripts to the folder, byte for byte as .NET Framework has them.
    """;

if (args.Length != 3 || (args[0] != "install" && args[0] != "export"))
{
    Console.Error.WriteLine(usage);
    return 2;
}
string[] files = args[1] switch
{
    "persistence" => SqlScripts.Persistence,
    "tracking" => SqlScripts.Tracking,
    "all" => [.. SqlScripts.Persistence, .. SqlScripts.Tracking],
    _ => [],
};
if (files.Length == 0)
{
    Console.Error.WriteLine(usage);
    return 2;
}
if (args[0] == "export")
{
    Directory.CreateDirectory(args[2]);
    foreach (string file in files)
    {
        File.WriteAllBytes(Path.Combine(args[2], file), SqlScripts.ReadBytes(file));
        Console.WriteLine(Path.Combine(args[2], file));
    }
    return 0;
}
SqlScripts.Run(args[2], files, Console.Out);
return 0;
