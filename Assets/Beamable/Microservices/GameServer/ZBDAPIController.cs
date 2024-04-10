using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using Beamable.Server;
using Beamable.Common;
using System.Net;
using Beamable.Server.Api.RealmConfig;
using static Beamable.Common.Constants.Features;

public static class ZBDAPIController
{
    public static async Task<SendToUsernameResponse> SendToUsername(string username, int amount, string description, string apikey)
    {

        SendToUsernamePayload request = new SendToUsernamePayload();
        request.gamertag = username;
        request.amount = (amount * 1000) + "";
        request.description = description;

        string jsonData = JsonConvert.SerializeObject(request);

        BeamableLogger.Log("send to user name payload: " + jsonData);

        HttpContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");


        HttpClient httpClient = new HttpClient();
        string url = "https://api.zebedee.io/v0/gamertag/send-payment";

        httpClient.DefaultRequestHeaders.Add("apikey", apikey);


        var response = await httpClient.PostAsync(url, content);
        SendToUsernameResponse sendResponse = new SendToUsernameResponse();
        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            BeamableLogger.Log("responseBody", responseBody);
            sendResponse = JsonConvert.DeserializeObject<SendToUsernameResponse>(responseBody);
            return sendResponse;
        }
        else
        {
            BeamableLogger.Log("error sending to username");
            sendResponse.success = false;
            try
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                sendResponse.message = responseBody;

            }
            catch (Exception e)
            {
                sendResponse.message = e.ToString();
            }

            return sendResponse;
        }
    }


    public static async Task<string> GetApiRegionResponse(string ipAddress , string apikey)
    {
        try
        {
            

            string url = $"https://api.zebedee.io/v0/is-supported-region/{ipAddress}";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("apikey", apikey);

                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string responseData = await response.Content.ReadAsStringAsync();
                    dynamic jsonData = JsonConvert.DeserializeObject(responseData);
                    string serializedJson = JsonConvert.SerializeObject(jsonData);

                    return serializedJson;
                }
                else
                {
                    throw new Exception("Failed to fetch data. Status code: " + response.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception("An error occurred: " + ex.Message);
        }
    }


}


