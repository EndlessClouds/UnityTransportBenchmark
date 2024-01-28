using System;

public static class LaunchArgUtility
{
    private static readonly string[] Args = Environment.GetCommandLineArgs();

    public static bool TryGetArg(string key, out string value)
    {
        for (var i = 0; i < Args.Length - 1; i++)
        {
            var arg = Args[i];

            if (arg.Equals(key))
            {
                var nextArg = Args[i + 1];
                if (nextArg.StartsWith('-'))
                {
                    continue;
                }

                value = nextArg;
                return true;
            }
        }

        value = default;
        return false;
    }

    public static bool HasArg(string key)
    {
        foreach (var s in Args)
        {
            if (s.Equals(key))
            {
                return true;
            }
        }

        return false;
    }
}