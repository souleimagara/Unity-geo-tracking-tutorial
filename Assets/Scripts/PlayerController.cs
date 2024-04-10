using Beamable;
using Beamable.Common.Api.Auth;
using Beamable.Serialization.SmallerJSON;
using Beamable.Server.Clients;
using Beamable.Theme.Palettes;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;


public class PlayerController : MonoBehaviour
{

    [Header("Player data")]
    public Text CountryCode;
    public Text CountryCodeDevice;
    public Text Continent;
    public Text AdressIP;
    public Text DeviceName;
    public Text TimeZone;
    public Text Local;
    public Text others;


    [Header("Beamable data")]
    public Text beamableGamerTagLabel;
    public Text CountryCodeBe;
    public Text ContinentB;


    [Header("Region")]
    public Text Regionmessage;


    [Header("ZBD Prefab")]
    public GameObject ZbdPrefab;

    string beamableGamerTag;
    long beamableGamerTaglong;

    string timeZone;
    string location;
    string countryCode;
    string CountryCodeBemable;

    string newipadress;

    private string apiKey = "AIzaSyA2Ap9tS2CGEjvZF4CcgIeImH6DedGS4nc";
    IEnumerator GetTimeZoneWithname()
    {
        UnityWebRequest www = UnityWebRequest.Get("https://ipinfo.io/json");
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string response = www.downloadHandler.text;
            TimeZoneDetails ipData = JsonUtility.FromJson<TimeZoneDetails>(response);

            if (ipData != null)
            {
                 timeZone = ipData.timezone;
                 location = ipData.loc;
                Debug.Log("Location: " + location);
                Local.text = location;
                Debug.Log("Time Zone: " + timeZone);
               // TimeZone.text = timeZone;
                 countryCode = ipData.country;
                Debug.Log("Country Code: " + countryCode);
                                
                Debug.Log("Country Code: " + ipData.region  + ipData.city  + ipData.postal);
                others.text = ipData.region + " "+  ipData.city + " " + ipData.postal;
                 newipadress = ipData.ip;
                Debug.Log("IP adressssssssssssssss " + newipadress);

              

            }
            else
            {
                Debug.Log("Failed to fetch IP information.");
                TimeZone.text = "Failed to fetch IP information.";
            }
        }
        else
        {
            Debug.Log("Failed to fetch IP information: " + www.error);
            TimeZone.text = "Failed to fetch IP information."+ www.error;
        }
    }

    public  async void SavePlayerStats(string countrycode , string local , string timezone , string ipadress , string devicename)
    {
        var beamContext = BeamContext.Default;
        await beamContext.OnReady;
        string access = "public";


        Dictionary<string, string> statsDictionary = new Dictionary<string, string>()
        {
        { "country_code",countrycode},
        { "local", local },
        { "timezone",timezone },
        { "ipadress", ipadress },
        { "devicename", devicename }
       
        };

        await beamContext.Api.StatsService.SetStats(access, statsDictionary);
        Debug.Log("done ");
      
    }
    public async void GetTimeZone()
    {

      
        //  GetTimeZoneWithoutName();
        StartCoroutine(GetTimeZoneWithname());
        await GetDeviceIPAddressAsync();
        GetUserDeviceName();


    }


    public void GetUserDeviceName()
    {
        string deviceModel = "";

#if UNITY_ANDROID
        deviceModel = SystemInfo.deviceModel;
#elif UNITY_IOS
            deviceModel = UnityEngine.iOS.Device.generation.ToString();
#else
            deviceModel = SystemInfo.deviceName;
#endif

        Debug.Log("Device Model: " + deviceModel);
        DeviceName.text = deviceModel; 
    }

    // Function to get the IP address of the device
    public async Task GetDeviceIPAddressAsync()
    {
        string ipAddress = "";

        try
        {
            // Get the IP address using system APIs
            ipAddress = GetIPAddressUsingSystemAPI();
            AdressIP.text = newipadress; ;
            Debug.Log("Device IP Address: " + ipAddress);
            var ctx = BeamContext.Default;
            await ctx.OnReady;

       //     bool playerstatsresult = await ctx.Microservices().GameServer().IsItSupportedRegion(newipadress);
           // Debug.Log("this is a test " + playerstatsresult);
            //if( playerstatsresult)
            //{
            //    Regionmessage.text = "Region supported congratulations !";
            //    ZbdPrefab.SetActive(true);
            //}
            //else
            //{
            //    Regionmessage.text = "Region Not supported  !";
            //    ZbdPrefab.SetActive(false);

            //}
         
          
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error getting device IP address: " + ex.Message);
        }

        
    }


    // Get IP address using system APIs
    private string GetIPAddressUsingSystemAPI()
    {
        string ipAddress = "";
        try
        {
            // Get the local machine's IP addresses
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

            // Loop through the IP addresses and find the IPv4 address
            foreach (IPAddress ip in localIPs)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ip.ToString();
                    break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.Log("Error: " + e.Message);
        }

        return ipAddress;
    }

    // Example usage
    private async void Start()
    {

     //   GetBeamableGamerTag();
        GetTimeZone();
        GetLocalTimeZone();
        await GetCountryCode();
        GetCountryCodeFromlocal();
        var ctx = BeamContext.Default;
        await ctx.OnReady;

      //  string playerstatsresult = await ctx.Microservices().GameServer().SetSats(countryCode, location, timeZone,newipadress , DeviceName.text);
       // SavePlayerStats(countryCode, location, timeZone, newipadress, DeviceName.text);
        //Debug.Log(playerstatsresult);
    }
    void  GetCountryCodeFromlocal()
    {
        // Get the current culture info
        CultureInfo culture = CultureInfo.CurrentCulture;

        // Get the region code (country code) from the current culture
        RegionInfo region = new RegionInfo(culture.Name);

        // Get the country code
        string countryCode = region.TwoLetterISORegionName;

        Debug.Log("Country Code: " + countryCode);
        CountryCodeDevice.text = countryCode;
    }
    void  GetLocalTimeZone()
    {
        // Get the current system's timezone
        TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
        TimeZone.text = localTimeZone.DisplayName;

        // Print the timezone to the console
        Debug.Log("The current system's timezone is: " + localTimeZone.DisplayName);
    }
    public async void GetBeamableGamerTag()
    {
        try
        {
            var ctx = BeamContext.Default;
            await ctx.OnReady;
            beamableGamerTag = ctx.Api.User.id + "";
         
            beamableGamerTaglong = long.Parse(beamableGamerTag);
           // RequestPlayerStatd(beamableGamerTaglong);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            beamableGamerTag = "error getting beamable id";
        }
        beamableGamerTagLabel.text = "id: " + beamableGamerTag;

    }

    //async void RequestPlayerStatd(long userid)
    //{

    //    //var ctx = BeamContext.Default;
    //    //await ctx.OnReady;
      
    //    //    string playerstatsresult = await ctx.Microservices().GameServer().GetplayerStats(userid);


       
    //    //PlayerStats result = JsonConvert.DeserializeObject<PlayerStats>(playerstatsresult);
    //    //CountryCodeBe.text = result.Location;
    //    //ContinentB.text = result.GeoContinentCode;

    //    //CountryCodeBemable = result.Location;
    //    //Debug.Log(result.Location);
    //    //Debug.Log(result.GeoContinentCode);
      


    //}


    async Task GetCountryCode()
    {
        // First, check if the required permissions are granted
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            return;
        }

        // First, check if user has location service enabled
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("Location service is not enabled");
            CountryCode.text = "Location service is not enabled";
            return;
        }

        // Start service before querying location
        Input.location.Start();
        Debug.Log("Entered");

        // Wait until service initializes
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            maxWait--;
        }

        // Service didn't initialize in 20 seconds
        if (maxWait < 1)
        {
            Debug.Log("Timed out");
            CountryCode.text = "Timed out";
            return;
        }

        // Connection has failed
        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.Log("Unable to determine device location");
            CountryCode.text = "Unable to determine device location";
            return;
        }

        // Access granted and location value could be retrieved
        double latitude = Input.location.lastData.latitude;
        double longitude = Input.location.lastData.longitude;

        // Now you can use these coordinates to determine the country code
        string countryCode = await QueryCountryCode(latitude, longitude);
        Debug.Log("Country Code: " + countryCode);
        CountryCode.text = countryCode;

        // Stop service if there is no need to query location updates continuously
        Input.location.Stop();
    }

    async Task<string> QueryCountryCode(double latitude, double longitude)
    {
        string url = "https://nominatim.openstreetmap.org/reverse?format=json&lat=" + latitude + "&lon=" + longitude + "&zoom=18&addressdetails=1";

        TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("accept-language", "en");
            var operation = www.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Delay(100);
            }

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("wwwwwwww is " + www.downloadHandler.text);
                // Parse the response to get country code
                string countryCode = ParseCountryCode(www.downloadHandler.text);
                
                tcs.SetResult(countryCode);
            }
            else
            {
                tcs.SetException(new Exception("Error querying country code: " + www.error));
                CountryCode.text = "Error querying country code: " + www.error;
            }
        }

        return await tcs.Task;
    }
    string ParseCountryCode(string response)
    {
        try
        {
            // Parse the JSON response
            var jsonResponse = JsonUtility.FromJson<OSMResponse>(response);

            // Check if the response contains address details
            if (jsonResponse.address != null)
            {
                // Extract the country code and name
                string countryCode = jsonResponse.address.country_code;
                string countryName = jsonResponse.address.country;

                // Check if the country code and name are valid
                if (!string.IsNullOrEmpty(countryCode) && !string.IsNullOrEmpty(countryName))
                {

                    CountryCode.text = $"Country Code: {countryCode}, Country Name: {countryName}";
                    return $"Country Code: {countryCode}, Country Name: {countryName}";
                   
                }
            }

            // If country details not found in response
            Debug.Log("Country details not found in response.");
            CountryCode.text = "Country details not found in response.";
            return "Country details not found";
        }
        catch (Exception ex)
        {
            // If an exception occurs during parsing
            Debug.LogError("Error parsing country details: " + ex.Message);
            CountryCode.text = "Error parsing country details: " + ex.Message;
            return "Error parsing country details";
        }
    }





}

