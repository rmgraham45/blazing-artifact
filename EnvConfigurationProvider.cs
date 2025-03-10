using Microsoft.Extensions.Configuration;
using System;
using System.IO;

public class EnvConfigurationProvider : ConfigurationProvider
{
    public EnvConfigurationProvider(string path)
    {
        if (File.Exists(path))
        {
            var envFile = File.ReadAllLines(path);
            foreach (var line in envFile)
            {
                var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    Data[parts[0]] = parts[1];
                }
            }
        }
    }
}

public class EnvConfigurationSource : IConfigurationSource
{
    private readonly string _path;

    public EnvConfigurationSource(string path)
    {
        _path = path;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EnvConfigurationProvider(_path);
    }
}
