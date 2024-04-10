// Define classes to represent the JSON structure
public class ZBDRegionData
{
    public bool success;
    public Data data;
}

public class Data
{
    public string ipAddress;
    public bool isSupported;
    public string ipCountry;
    public string ipRegion;
}