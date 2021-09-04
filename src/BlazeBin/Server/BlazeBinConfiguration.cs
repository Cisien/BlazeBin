﻿using System.Diagnostics.CodeAnalysis;

namespace BlazeBin.Server;
public class BlazeBinConfiguration
{
    public string BaseDirectory { get; private set; } = "/app/data";

    public BlazeBinDataProtection DataProtection { get; } = new();
    public BlazeBinGrooming Grooming { get; } = new();
    public BlazeBinHosting Hosting { get; } = new();
    public BlazeBinStats Stats { get; } = new();
}

public class BlazeBinHosting
{
    public bool UseForwardedHeaders { get; private set; } = false;
    public List<List<string>> KnownNetworks { get; private set; } = new();
    public List<string> KnownProxies { get; private set; } = new();
    public string? ProtoHeadername { get; private set; }
    public string? ForwardedForHeaderName { get; private set; }
}

public class BlazeBinDataProtection
{
    public string? KeyIdentifier { get; private set; }
    public string? KeyLocation { get; private set; }

    [MemberNotNullWhen(true, nameof(KeyIdentifier), nameof(KeyLocation))]
    public bool Enabled { get; private set; } = false;
}

public class BlazeBinGrooming
{
    public bool Enabled { get; private set; } = false;
    public TimeSpan MaxAge { get; private set; } = TimeSpan.FromDays(30);
    public TimeSpan Interval { get; private set; } = TimeSpan.FromHours(1);
}

public class BlazeBinStats
{
    public bool Enabled { get; private set; } = false;
    public TimeSpan CollectionInterval { get; private set; } = TimeSpan.FromMinutes(1);
    public bool LogToMSEL { get; private set; } = false;
    public bool LogToAppInsights { get; private set; } = false;
}