using System;

[Serializable]
public class OSMResponse
{
    public AddressDetails address;
    public double lat;
    public double lon;
}

[Serializable]
public class AddressDetails
{
    public string country_code;
    public string country;
    // Add other address fields here if needed
}

[System.Serializable]
public class TimeZoneResponse
{
    public string status;
    public string message;
    public string countryCode;
    public string countryName;
    public string zoneName;
    public string abbreviation;
    public int gmtOffset;
    public int dstOffset;
    public int timestamp;
}
