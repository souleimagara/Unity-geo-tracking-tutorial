using Beamable;
using Beamable.Common.Api.Auth;
using Beamable.Server.Clients;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;
using UnityEngine.UI;
using static ZBD.Models;

public class GameManager : MonoBehaviour
{
    
    
    [Header("Player Data From Device")]
    [Header("Data From GPS")]
    public Text countryCodeGpsText;
    public Text localGpsText;
  

    [Header("Data From Device")]
    public Text countryCodeDeviceText;
    public Text timeZoneDeviceText;
    public Text deviceNameText;
    public Text localIpAddressText;



    [Header("Data From API")]
    public Text countryCodeApiText;
    public Text timeZoneApiText;
    public Text localeApiText;
    public Text publicIpAddressApiText;

    [Header("Player Data From Beamable")]
    public Text beamableGamerTagText;
    public Text beamableCountryCodeText;
    public Text beamableContinentText;
  


    [Header("Error Handler")]
    public Text errorHandlerText;


    [Header("ZBD Template")]
    public GameObject zbdTemplatePrefab;


    [Header("Warring  panel ")]
    public GameObject WarringPanel;
    public Text WarringText;


    string apiKey = "0DQSMRDUJ7T5";

    private long beamableGamerTaglong;

    string timeZone;

    async void Awake()
    {
        await StartGPS();
        GetBeamableGamerTag();

    }
    async void Start()
    {
        await GetCountryCodeFromGPS();
        CallAPI();
        GetDataFromDevice();
    


      Invoke("SaveDataToBeamableStat", 20);
      InvokeRepeating("CheckCountryCode", 30 , 20);
      //InvokeRepeating("CheckTimeZone", 35 , 30);




    }


    #region GPS Country Code & Local

    async Task StartGPS()
    {
        Permission.RequestUserPermission(Permission.FineLocation);

        int maxWait = 20;
        while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && maxWait > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            maxWait--;
        }

        if (maxWait < 1)
        {
            HandleError("Timed out while waiting for location permission.", "Permission");
            return;
        }

        if (!Input.location.isEnabledByUser)
        {
            HandleError("Location service is not enabled", "Permission");
            return;
        }

        Input.location.Start();
        // Continue with GPS operations
    }

    async Task GetCountryCodeFromGPS()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            return;
        }

        if (!Input.location.isEnabledByUser)
        {
            HandleError("Location service is not enabled", "Permission");
            return;
        }

        Input.location.Start();
        DisplayDebug("Entered !");
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            maxWait--;
        }

        if (maxWait < 1)
        {
            HandleError("Timed out while initializing location service", "Timing");
            return;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            HandleError("Unable to determine device location", "location status");
            return;
        }
     
        double latitude = Input.location.lastData.latitude;
        double longitude = Input.location.lastData.longitude;

        await QueryCountryCode(latitude, longitude);

        Input.location.Stop();
    }

    async Task QueryCountryCode(double latitude, double longitude)
    {
        DisplayDebug("Entered QueryCountryCode  !");
        string url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude}&lon={longitude}&zoom=18&addressdetails=1";

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
                DisplayDebug("Entered response  !" + www.downloadHandler.text);
                ParseCountryCodeAndLocal(www.downloadHandler.text);
              
            }
            else
            {
                HandleError($"Error querying country code: {www.error}", "country code");

            }
        }

    }

    async void ParseCountryCodeAndLocal(string response)
    {
        try
        {
            OSMResponse jsonResponse = JsonUtility.FromJson<OSMResponse>(response);

            if (jsonResponse != null)
            {
                string countryCode = jsonResponse.address != null ? jsonResponse.address.country_code : "";
                string countryName = jsonResponse.address != null ? jsonResponse.address.country : "";
                double latitude = jsonResponse.lat;
                double longitude = jsonResponse.lon;             
                if (!string.IsNullOrEmpty(countryCode) && !string.IsNullOrEmpty(countryName))
                {
                   
                    countryCodeGpsText.text = countryCode /*+ " Country Name: " + countryName*/;
                    localGpsText.text = $"Latitude: {latitude}, Longitude: {longitude}";
                   

                   await GetTimeZone(latitude, longitude);
                }
                else
                {
                    HandleError("Some details are empty please check the response", "Country_Locale");
                }
            }
            else
            {
                HandleError("Response object is null.", "Country_Locale");
            }
        }
        catch (Exception ex)
        {
            HandleError("Error parsing country details: " + ex.Message, "Country_Locale");
        }
    }

    async Task<string> GetTimeZone(double latitude, double longitude)
    {
      
        string timeZoneUrl = $"https://api.timezonedb.com/v2.1/get-time-zone?key={apiKey}&format=json&by=position&lat={latitude}&lng={longitude}";

      

        using (UnityWebRequest timeZoneRequest = UnityWebRequest.Get(timeZoneUrl))
        {
            var timeZoneOperation = timeZoneRequest.SendWebRequest();

            while (!timeZoneOperation.isDone)
            {
                await Task.Delay(100);
            }

            if (timeZoneRequest.result == UnityWebRequest.Result.Success)
            {
                TimeZoneResponse timeZoneResponse = JsonUtility.FromJson<TimeZoneResponse>(timeZoneRequest.downloadHandler.text);
                timeZone = timeZoneResponse?.zoneName;

                DisplayDebug("The time zone is " + timeZone);
            }
        }

        return timeZone;
    }







    #endregion

    #region Device Country Code & Time Zone & Device name & Local IP address
    public void GetDataFromDevice()
    {
        GetCountryCodeFromDevice();
        GetUserDeviceName();      
        GetTimeZoneFromDevice();     
        GetIPAddressUsingSystemAPI();
    }

     void GetCountryCodeFromDevice()
     {
        CultureInfo culture = CultureInfo.CurrentCulture;
        RegionInfo region = new RegionInfo(culture.Name);
        string countryCode = region.TwoLetterISORegionName;

        countryCodeDeviceText.text = countryCode;
        
 
     }

     void GetTimeZoneFromDevice()
     {
        TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
        timeZoneDeviceText.text = localTimeZone.DisplayName;      
   
     }

    void GetUserDeviceName()
    {
        string deviceModel = "";


        deviceModel = SystemInfo.deviceModel;

        deviceNameText.text = deviceModel;

    }

     void GetIPAddressUsingSystemAPI()
    {
        try
        {
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

            foreach (IPAddress ip in localIPs)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    string ipAddress = ip.ToString();
                    localIpAddressText.text = ipAddress;
                   
                    break;
                }
            }
        }
        catch (Exception e)
        {
            HandleError("Error: " + e.Message, "local IP address");
        }
    }
    #endregion

    #region Post Request to get public IP address & time zone & local
    async void CallAPI()
    {
      await PostRequestToInfoAsync();
    }

    async Task PostRequestToInfoAsync()
    {
        UnityWebRequest www = UnityWebRequest.Get("https://ipinfo.io/json");
        var operation = www.SendWebRequest();
        while (!operation.isDone)
            await Task.Yield();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string response = www.downloadHandler.text;
            TimeZoneDetails ipData = JsonUtility.FromJson<TimeZoneDetails>(response);

            if (ipData != null)
            {
                countryCodeApiText.text = ipData.country;
                timeZoneApiText.text = ipData.timezone;
                localeApiText.text = ipData.loc;
                publicIpAddressApiText.text = ipData.ip;

                DetectSupportedRegionFromZBD(ipData.ip);

            }
            else
            {
                HandleError("Failed to fetch IP information.", "Post request");
            }
        }
        else
        {
            HandleError("Failed to fetch IP information: " + www.error, "Post request");
        }
    }
    #endregion



    #region Get Player Data from Beamable 


    public async void GetBeamableGamerTag()
    {
        try
        {
            var ctx = BeamContext.Default;
            await ctx.OnReady;
           

            beamableGamerTaglong = long.Parse(ctx.Api.User.id + "");
            
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            HandleError("error getting beamable id" + e.Message, "beamable gamer tag");
            
        }
        beamableGamerTagText.text = "id: " + beamableGamerTaglong;
        RequestPlayerStatsFromServer(beamableGamerTaglong);
       

    }

    async void RequestPlayerStatsFromServer(long userid)
    {

        var ctx = BeamContext.Default;
        await ctx.OnReady;
        string playerstatsresult = await ctx.Microservices().GameServer().GetplayerStats(userid);
        PlayerStats result = JsonConvert.DeserializeObject<PlayerStats>(playerstatsresult);
        beamableCountryCodeText.text = result.CountryCode;
        beamableContinentText.text = result.GeoContinentCode;

    }

    #endregion


    #region Saving data to beamable stats
    private async Task SaveDataToBeamableStat()
    {

        if (await CanSaveData())
        {
            await SaveStat("api_country_code", countryCodeApiText.text);
            await SaveStat("api_timezone", timeZoneApiText.text);
            await SaveStat("api_public_ipadress", publicIpAddressApiText.text);
            await SaveStat("api_locale", localeApiText.text);

            await SaveStat("local_ip_adress", localIpAddressText.text);
            await SaveStat("device_name", deviceNameText.text);
            await SaveStat("device_timezone", timeZoneDeviceText.text);
            await SaveStat("device_country_code", countryCodeDeviceText.text);

            await SaveStat("gps_country_code", countryCodeGpsText.text);
            await SaveStat("gps_local", localGpsText.text);
            await SaveStat("gps_timezone", timeZone);


            DisplayDebug("Player Data  saved to beamable ");
            HandleError("Player Data  saved to beamable ", "Saving data to stats");
        }
        else
        {
            HandleError("Player Data Already saved to beamable ", "Saving data to stats");
            DisplayDebug("Player Data Already saved to beamable ");
        }

    }


    async public Task<bool> CanSaveData()
    {
        var ctx = BeamContext.Default;
        await ctx.OnReady;
        bool playerstatsresult = await ctx.Microservices().GameServer().CanSaveData(beamableGamerTaglong);
        return playerstatsresult;

    }
    private async Task SaveStat(string statKey, string value)
    {
        var context = BeamContext.Default;
        await context.OnReady;

        Debug.Log($"context.PlayerId = {context.PlayerId}");

        string access = "private";

        // Set Value
        Dictionary<string, string> stat = new Dictionary<string, string>() { { statKey, value } };

        await context.Api.StatsService.SetStats(access, stat);


    }

    #endregion


    #region ZBD Region Support from beamable

    public async void DetectSupportedRegionFromZBD(string ipadress)
    {
       
        var ctx = BeamContext.Default;
        await ctx.OnReady;
        string supportedregionresult = await ctx.Microservices().GameServer().IsItSupportedRegion(ipadress);
       
        ZBDRegionData parsedResponse = JsonConvert.DeserializeObject<ZBDRegionData>(supportedregionresult);
     
        // Check if the response indicates success
        if (parsedResponse.success)
        {
            
            zbdTemplatePrefab.SetActive(true);
        }
        else
        {
         
            zbdTemplatePrefab.SetActive(false);
        }

    
       
    }

    #endregion


    #region Sending alerts

    async void CheckCountryCode()
    {
        var ctx = BeamContext.Default;
        await ctx.OnReady;




        (bool result, string message) = await ctx.Microservices().GameServer().checkingCountryCode(beamableGamerTaglong, countryCodeApiText.text);

        // Display message based on result
        if (result)
        {

           
            ShowMessageToUser(message);
        }
        else
        {

          
            // Show message to the user
            ShowMessageToUser(message);
        }



    }

    async void CheckTimeZone()
    {
        var ctx = BeamContext.Default;
        await ctx.OnReady;




        (bool result, string message) = await ctx.Microservices().GameServer().checkingtimezone(beamableGamerTaglong, timeZoneApiText.text);

       
        // Display message based on result
        if (result)
        {

           
            ShowMessageToUser(message);
        }
        else
        {

         
            // Show message to the user
            ShowMessageToUser(message);
        }



    }

    // Method to show message to the user (You need to implement this according to your UI)
    void ShowMessageToUser(string message)
    {
        WarringPanel.SetActive(true);
        WarringText.text = message;
    }

    #endregion


    #region Error handler & display debug
    void HandleError(string errorMessage, string type)
    {
        Debug.LogError(errorMessage + " " + type);
        errorHandlerText.text = errorMessage + " from " + type;
    }
    void DisplayDebug(string debugMessage)
    {
        Debug.Log(debugMessage);
    }
    #endregion
  



}
