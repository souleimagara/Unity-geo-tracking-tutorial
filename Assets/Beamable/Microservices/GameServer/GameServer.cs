using Beamable.Server;
using Newtonsoft.Json;
using System;
using Beamable.Server.Api.RealmConfig;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Beamable.Common;
using System.Text;
using System.Collections.Generic;
using Beamable.Common.Inventory;



namespace Beamable.Microservices
{

    [Microservice("GameServer")]
    public class GameServer : Microservice
    {
        string satsKey = "currency.sats";
        string rewardedTimeKey = "currency.rewardedTime";
        string lastPlayIntegrityCheckKey = "currency.lastPlayIntegrityCheck";
        string playerdatasaved = "currency.playedatasave";
        string whitelistedId = "items.whitelisted";
        string validatedId = "items.validated";
        string blacklistedId = "items.blacklisted";
      
        private int rewardAmount;



        [ClientCallable]
        public async Task<string> GetStats()
        {
            RewardsResponse rewardRes = new RewardsResponse();
            long currentSats = await Services.Inventory.GetCurrency(satsKey);
            long currentRewardedTime = await Services.Inventory.GetCurrency(rewardedTimeKey);
            RealmConfig config = await Services.RealmConfig.GetRealmConfigSettings();

            long rewardTime = long.Parse(config.GetSetting("accounts", "rewardTime", "5"));

            rewardRes.currentSats = currentSats;
            rewardRes.currentTimePlayed = currentRewardedTime;
            rewardRes.currentRequiredTime = rewardTime;
            return JsonConvert.SerializeObject(rewardRes);
        }

        // Google Play integrity doesnt like it if you call too often, so check if its time to call
        [ClientCallable]
        public async Task<bool> ShouldVerify()
        {
            DateTime currentTimeUtc = DateTime.UtcNow;

            long currentTimeStampSeconds = (long)(currentTimeUtc.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            long lastPlayIntegrityCheck = await Services.Inventory.GetCurrency(lastPlayIntegrityCheckKey);

            long timeSinceCheck = currentTimeStampSeconds - lastPlayIntegrityCheck;

            /* If the time since the last check is greater than 30 mins check again
             If lastPlayIntegrityCheck is 0 assume its the first check */
            if (timeSinceCheck > 1800 || lastPlayIntegrityCheck == 0)
            {
                return true;
            }

            return false;
        }
        private async Task<string> GetPlayerCountryCodeAsync(long userid)
        {
            try
            {
                // Retrieve the player's country code asynchronously
                string countryCode = await Services.Stats.GetProtectedPlayerStat(userid, "location");
                return countryCode;
            }
            catch (Exception ex)
            {
                // Handle exceptions if necessary
                // For example, log the error or return a default value
                BeamableLogger.LogError("Error retrieving player's country code: " + ex.Message);
                return null;
            }
        }

        [ClientCallable]
        public async Task<string> SendPlaytime(string integrityToken, string payload , long userid)
        {
            BeamableLogger.Log("payload " + payload);
            RewardsResponse rewardRes = new RewardsResponse();

            try
            {
                bool isWhiteListed = await IsWhitelisted();
                bool isBlackListed = await IsBlacklisted();
                bool isValidated = await IsValidated();
                long currentSats = await Services.Inventory.GetCurrency(satsKey);
                long currentRewardedTime = await Services.Inventory.GetCurrency(rewardedTimeKey);

                rewardRes.validated = isValidated;
                rewardRes.whitelisted = isWhiteListed;
                rewardRes.currentSats = currentSats;
                rewardRes.currentTimePlayed = currentRewardedTime;
                rewardRes.blacklisted = isBlackListed;

                // Get Config constants and check if they are set correctly

                RealmConfig config = await Services.RealmConfig.GetRealmConfigSettings();

                long rewardTime = long.Parse(config.GetSetting("accounts", "rewardTime", "15"));
               
                rewardRes.currentRequiredTime = rewardTime;
                // Wait for the task to get the player's country code to complete
                string Playercountrycode = await GetPlayerCountryCodeAsync(userid);
                if (!string.IsNullOrEmpty(Playercountrycode))
                {
                    switch (Playercountrycode)
                    {
                        case "US":
                            rewardAmount = int.Parse(config.GetSetting("countries_code_reward", "US", "50"));
                            break;
                        case "TN":
                            rewardAmount = int.Parse(config.GetSetting("countries_code_reward", "TN", "20"));
                            break;                   
                        default:
                            // Handle other country codes or unknown codes here
                            rewardAmount = int.Parse(config.GetSetting("accounts", "rewardAmount", "3"));
                            break;
                    }

                }
                else
                {
                    rewardRes.error = true;
                    rewardRes.message = "Failed to retrieve player's country code";
                    return JsonConvert.SerializeObject(rewardRes);
                }


                string missingConfigs = "Please add the following to your beamable Config ";
                bool isMissingConfig = false;
                //string quagoUsername = config.GetSetting("accounts", "quagoUsername", "");
                //string quagoPassword = config.GetSetting("accounts", "quagoPassword", "");
                //string quagoClientId = config.GetSetting("accounts", "quagoClientId", "");
                //string quagoAppToken = config.GetSetting("accounts", "quagoAppToken", "");
                string serviceJSON = config.GetSetting("accounts", "serviceJSON", "");
                string zbdApiKey = config.GetSetting("accounts", "zbdApiKey", "");


                if (serviceJSON == "")
                {
                    isMissingConfig = true;
                    missingConfigs += "serviceJSON";
                }

                if (zbdApiKey == "")
                {
                    isMissingConfig = true;
                    missingConfigs += "zbdApiKey";
                }

                if (isMissingConfig)
                {
                    rewardRes.error = true;
                    rewardRes.message = missingConfigs;
                    return JsonConvert.SerializeObject(rewardRes);
                }

                // If not using Quago remove these checks
                //if (quagoUsername == "")
                //{
                //    isMissingConfig = true;
                //    missingConfigs += "quagoUsername ";
                //}
                //if (quagoPassword == "")
                //{
                //    isMissingConfig = true;
                //    missingConfigs += "quagoPassword ";
                //}
                //if (quagoClientId == "")
                //{
                //    isMissingConfig = true;
                //    missingConfigs += "quagoClientId ";
                //}
                //if (quagoAppToken == "")
                //{
                //    isMissingConfig = true;
                //    missingConfigs += "quagoAppToken ";
                //}

                /* Get the game play data from the payload
                 Here were are doing a very basic check to see if the player has touched the screen
                 And if the device has moved i.e. is in human hands
                 You can and should replace this with specific checks for your game
                 */

                GameData gameData = JsonConvert.DeserializeObject<GameData>(payload);

                if (gameData.touchCounts == 0)
                {
                    rewardRes.error = true;
                    rewardRes.message = "no touches detected";
                    return JsonConvert.SerializeObject(rewardRes);
                }

                if (gameData.accelerometerCount == 0)
                {

                    rewardRes.error = true;
                    rewardRes.message = "no movement detected";
                    return JsonConvert.SerializeObject(rewardRes);
                }


                // Google play integrity can cause false positives so we can allow users who fail to bypass this check by assigning them the 'whitelisted' item
                if (!isWhiteListed)
                {
                    // We can call play integrity too often so check if its time
                    bool shouldVerify = await ShouldVerify();
                    if (shouldVerify)
                    {
                        /* Check if the device and app are valid via Google Play Integrity */
                        string res = await PlayIntegrityController.RunPlayIntegrity(serviceJSON, integrityToken);

                        DateTime currentTimeUtc = DateTime.UtcNow;
                        long currentTimeStampSeconds = (long)(currentTimeUtc.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                        await Services.Inventory.SetCurrency(lastPlayIntegrityCheckKey, currentTimeStampSeconds);
                        BeamableLogger.Log("play integrity " + res);
                        RootObject rootObject = JsonConvert.DeserializeObject<RootObject>(res);

                        string payloadHash = ComputeSha256Hash(payload);
                        string requestHash = rootObject.TokenPayloadExternal.RequestDetails.RequestHash;


                        string deviceIntegrity = rootObject.TokenPayloadExternal.DeviceIntegrity.DeviceRecognitionVerdict[0];
                        string appIntegrity = rootObject.TokenPayloadExternal.AppIntegrity.AppRecognitionVerdict;


                        // The payload sent from the client to this microservice has been altered or is fake
                        //if (payloadHash != requestHash)
                        //{
                        //    if (!isBlackListed)
                        //    {
                        //        await Services.Inventory.AddItem(blacklistedId);
                        //    }
                        //    rewardRes.blacklisted = true;
                        //    rewardRes.error = true;
                        //    rewardRes.message = "tampering detected";
                        //    return JsonConvert.SerializeObject(rewardRes);
                        //}

                        // The app is not genuine, possibly installed outside of google play or tampered with
                        //if (appIntegrity != "PLAY_RECOGNIZED")
                        //{
                        //    if (!isBlackListed)
                        //    {
                        //        await Services.Inventory.AddItem(blacklistedId);
                        //    }
                        //    rewardRes.blacklisted = true;
                        //    rewardRes.error = true;
                        //    rewardRes.message = "failed app integrity";
                        //    return JsonConvert.SerializeObject(rewardRes);
                        //}

                        // The device has a high chance of being fake or is rooted
                        //if (!deviceIntegrity.Contains("MEETS_STRONG_INTEGRITY"))
                        //{
                        //    if (!isBlackListed)
                        //    {
                        //        await Services.Inventory.AddItem(blacklistedId);
                        //    }
                        //    rewardRes.blacklisted = true;
                        //    rewardRes.error = true;
                        //    rewardRes.message = "failed device integrity";
                        //    return JsonConvert.SerializeObject(rewardRes);
                        //}

                        if (!isValidated)
                        {
                            await Services.Inventory.AddItem(validatedId);
                        }
                    }
                }



                /* Calculate the playtime from the Android App Usage Stats library */

                AppUsageStats usageStats = gameData.appUsageStats;

                // Convert the milliseconds into minutes
                long totalPlaytimeMins = (usageStats.totalTimeInForeground / 1000) / 60;


                // This can happen if the app was reinstalled, beamable remembers time played but app usage stats has not record anymore, so reset currentRewardTime to 0
                if (totalPlaytimeMins < currentRewardedTime)
                {
                    currentRewardedTime = 0;
                    await Services.Inventory.SetCurrency(rewardedTimeKey, currentRewardedTime);
                }


                /* If you are not using Quago anti cheat you should remove this section*/
               // PlayerInfo quagoData = await QuagoController.GetQuagoData(gameData.userId, quagoUsername, quagoPassword, quagoClientId, quagoAppToken);

                // Quago could probably not find an entry for the user yet
                //if (quagoData.success == false)
                //{
                //    rewardRes.error = true;
                //    rewardRes.message = quagoData.error;
                //    return JsonConvert.SerializeObject(rewardRes);
                //}

                //// Quago has detected that the user has not played
                //if (quagoData == null || quagoData.TotalPlaytimeHours == 0)
                //{
                //    rewardRes.error = true;
                //    rewardRes.message = "no quago data";
                //    return JsonConvert.SerializeObject(rewardRes);
                //}

                //// Return an error if Quago has detected any fake gamer play or game play on an emulator
                //if (quagoData.InauthPlaytimePercentage > 0 || quagoData.EmuPlaytimePercentage > 0)
                //{
                //    if (!isBlackListed)
                //    {
                //        await Services.Inventory.AddItem(blacklistedId);
                //    }
                //    rewardRes.blacklisted = true;

                //    rewardRes.error = true;

                //    if (quagoData.InauthPlaytimePercentage > 0)
                //    {
                //        rewardRes.message = "Inauthentic playing detected\n";
                //    }
                //    if (quagoData.EmuPlaytimePercentage > 0)
                //    {
                //        rewardRes.message = "Emulators are not allowed";
                //    }

                //    return JsonConvert.SerializeObject(rewardRes);
                //}




                // Calculate the current time played since the last reward, if it greater than `rewardTime` assign sats balance

                long currentPlayTimeMins = totalPlaytimeMins - currentRewardedTime;

                if (currentPlayTimeMins >= rewardTime)
                {
                    currentSats += rewardAmount;
                    currentRewardedTime = totalPlaytimeMins;
                    currentPlayTimeMins = 0;
                    await Services.Inventory.SetCurrency(satsKey, currentSats);
                    await Services.Inventory.SetCurrency(rewardedTimeKey, currentRewardedTime);

                }


                rewardRes.currentRequiredTime = rewardTime;
                rewardRes.currentSats = currentSats;
                rewardRes.currentTimePlayed = currentPlayTimeMins;

                return JsonConvert.SerializeObject(rewardRes);

            }
            catch (Exception e)
            {

                BeamableLogger.LogError(e);

                rewardRes.error = true;
                rewardRes.message = e + "";
                return JsonConvert.SerializeObject(rewardRes);
            }
        }

        async Task<bool> IsWhitelisted()
        {
            var items = await Services.Inventory.GetItems<ItemContent>();


            foreach (var item in items)
            {
                if (item.ItemContent.Id == whitelistedId)
                {
                    return true;
                }
            }
            return false;
        }

        async Task<bool> IsBlacklisted()
        {
            var items = await Services.Inventory.GetItems<ItemContent>();


            foreach (var item in items)
            {
                if (item.ItemContent.Id == blacklistedId)
                {
                    return true;
                }
            }
            return false;
        }

        async Task<bool> IsValidated()
        {
            var items = await Services.Inventory.GetItems<ItemContent>();


            foreach (var item in items)
            {
                if (item.ItemContent.Id == validatedId)
                {
                    return true;
                }
            }
            return false;
        }

        public static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        [ClientCallable]
        public async Task<string> WithdrawBitcoin(string username, string integrityToken)
        {

            long currentSats = await Services.Inventory.GetCurrency(satsKey);
            await Services.Inventory.SetCurrency(satsKey, 0);

            SendToUsernameResponse res = new SendToUsernameResponse();
            if (currentSats == 0)
            {
                res.success = false;
                res.message = "You have not earned enough bitcoin to withdraw";
                return JsonConvert.SerializeObject(res);
            }


            RealmConfig config = await Services.RealmConfig.GetRealmConfigSettings();

            string apikey = config.GetSetting("accounts", "zbdApiKey", "");
            string serviceJSON = config.GetSetting("accounts", "serviceJSON", "");

            if (apikey.Length == 0)
            {
                res.success = false;
                res.message = "zbdApiKey not set in beamable dashboard";
                await Services.Inventory.SetCurrency(satsKey, currentSats);
                return JsonConvert.SerializeObject(res);
            }

            if (serviceJSON.Length == 0)
            {
                res.success = false;
                res.message = "serviceJSON not set in beamable dashboard";
                await Services.Inventory.SetCurrency(satsKey, currentSats);
                return JsonConvert.SerializeObject(res);
            }

            bool isWhiteListed = await IsWhitelisted();


            if (!isWhiteListed)
            {

                /* Check if the device and app are valid via Google Play Integrity */
                string playIntegrityRes = await PlayIntegrityController.RunPlayIntegrity(serviceJSON, integrityToken);
                BeamableLogger.Log("play integrity " + res);
                RootObject rootObject = JsonConvert.DeserializeObject<RootObject>(playIntegrityRes);

                string deviceIntegrity = rootObject.TokenPayloadExternal.DeviceIntegrity.DeviceRecognitionVerdict[0];
                string appIntegrity = rootObject.TokenPayloadExternal.AppIntegrity.AppRecognitionVerdict;

                // The app is not genuine, possibly installed outside of google play or tampered with
                //if (appIntegrity != "PLAY_RECOGNIZED")
                //{
                //    res.success = false;
                //    res.message = "failed app integrity";
                //    return JsonConvert.SerializeObject(res);
                //}

                // The device has a high chance of being fake or is rooted
                //if (!deviceIntegrity.Contains("MEETS_STRONG_INTEGRITY"))
                //{
                //    res.success = false;
                //    res.message = "failed device integrity";
                //    return JsonConvert.SerializeObject(res);
                //}
            }


            res = await ZBDAPIController.SendToUsername(username, (int)currentSats, "Withdrawal", apikey);


            if (!res.success)
            {
                await Services.Inventory.SetCurrency(satsKey, currentSats);

            }


            long rewardTime = long.Parse(config.GetSetting("accounts", "rewardTime", "5"));

            long currentRewardedTime = await Services.Inventory.GetCurrency(rewardedTimeKey);
            res.currentRequiredTime = rewardTime;
            res.currentSats = 0;
            res.currentTimePlayed = 0;
            return JsonConvert.SerializeObject(res);

        }
        [ClientCallable]
        public async Task<string> GetplayerStats(long useris)
        {
            
           
            string[] statslist = new string[] { "location", "geo_continent_code" };
            Dictionary<string, string> statsDictionary = await Services.Stats.GetProtectedPlayerStats(useris, statslist);

            PlayerStats playerStats = new PlayerStats
            {
                CountryCode = statsDictionary["location"],
                GeoContinentCode = statsDictionary["geo_continent_code"]
            };         
            string serializedStats = JsonConvert.SerializeObject(playerStats);
           
            return serializedStats;

        }

        [ClientCallable]
        public async Task<string> IsItSupportedRegion(string ipAddress)
        {
            try
            {
               
                RealmConfig config = await Services.RealmConfig.GetRealmConfigSettings();
                string apiKey = config.GetSetting("accounts", "zbdApiKey", "");
              
               string res = await ZBDAPIController.GetApiRegionResponse(ipAddress, apiKey);
                BeamableLogger.Log("region data is " + res);
                return res;
               
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred: " + ex.Message);
            }
        }




        [ClientCallable]
        public async Task<bool> CanSaveData(long userid)
        {
          
            long playerdatasave = await Services.Inventory.GetCurrency(playerdatasaved);
            BeamableLogger.Log("player data saved " + playerdatasave);
            if (playerdatasave == 1 )
            {
                BeamableLogger.Log("data already saved ");
                return false;
            }
            else 
            {

                await Services.Inventory.SetCurrency(playerdatasaved , 1);
                BeamableLogger.Log("data  saved ");
                return true;
            }
          
        }
        [ClientCallable]
        public async Task<(bool, string)> CheckCheating(long userid, params (string propertyName, string propertyValue)[] properties)
        {
            string result = "";
            // This code executes on the server.
            string[] deviceNames = new string[] { "device_country_code", "device_timezone" , "gps_country_code"  , "gps_local" , "api_country_code" , "api_locale" , "api_public_ipadress" };
            Dictionary<string, string> stats = await Services.Stats.GetStats("client", "private", "player", userid, deviceNames);
            // Assuming you want to retrieve the value for the key "device_name_private"
            if (stats.ContainsKey("device_country_code"))
            {
                string value = stats["device_country_code"];
                BeamableLogger.Log("the value is " + value);
            }
            else
            {
                BeamableLogger.Log("Device name not found in stats.");
            }
           


            return (false, result);
        }

        [ClientCallable]
        public async Task<(bool, string)> checkingCountryCode (long userid , string countrycodeapi)
        {
            string result = "";

            string[] deviceNames = new string[] { "gps_country_code" };
            Dictionary<string, string> stats = await Services.Stats.GetStats("client", "private", "player", userid, deviceNames);

            if (stats.ContainsKey("gps_country_code"))
            {
                  string value = stats["gps_country_code"];
                 if( countrycodeapi.ToUpper() != value.ToUpper()) {

                    result = "Country code does not match.  Please check support !";
                    bool isBlackListed = await IsBlacklisted();
                    if (!isBlackListed)
                    {
                        await Services.Inventory.AddItem(blacklistedId);
                    }
                        return (false, result);
                   
                 }
                 else if(countrycodeapi.ToUpper() == value.ToUpper())
                 {
                    result = "Country code  match.";
                    return (true, result);
                }
            }
            else
            {
                BeamableLogger.Log("Failed to obtain data from Beamable stats. Please try again later.");
                result = "Failed to obtain data from Beamable stats. Please try again later.";
                return (false, result);
            }

            return (false, result);
        }


        [ClientCallable]
        public async Task<(bool, string)> checkingtimezone(long userid, string timezoneapi)
        {
            string result = "";
            string[] deviceNames = new string[] { "gps_timezone" };
            Dictionary<string, string> stats = await Services.Stats.GetStats("client", "private", "player", userid, deviceNames);

            if (stats.ContainsKey("gps_timezone"))
            {
                string value = stats["gps_timezone"];
                if (timezoneapi.ToUpper() != value.ToUpper())
                {
                   
                  await Services.Inventory.AddItem(whitelistedId);
                    
                    result = "Time zone code does not match. Please check support !";
                    return (false, result);

                }
                else if (timezoneapi.ToUpper() == value.ToUpper())
                {
                    result = "Time zone code  match.";
                    return (true, result);
                }
            }
            else
            {
                BeamableLogger.Log("Failed to obtain data from Beamable stats. Please try again later.");
                result = "Failed to obtain data from Beamable stats. Please try again later.";
                return (false, result);
            }

            return (false, result);
        }



    }



}
