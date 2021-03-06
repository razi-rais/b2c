﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using AADB2C.JITUserMigration.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace AADB2C.JITUserMigration.Controllers
{
    
    public class UserMigrationController : ApiController
    {
       
        // Demo: Inject an instance of an AppSettingsModel class into the constructor of the consuming class, 
        // and let dependency injection handle the rest
        //public UserMigrationController(IOptions<AppSettingsModel> appSettings)
        //{
        //    this.AppSettings = appSettings.Value;
        //}

        [System.Web.Http.HttpPost]
        public IHttpActionResult Migrate()
        {
            string input = Request.Content.ReadAsStringAsync().Result;

            // If not data came in, then return
            if (this.Request.Content == null)
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("Request content is null", HttpStatusCode.Conflict));
            }

            //// Read the input claims from the request body
            //using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            //{
            //    input = await reader.ReadToEndAsync();
            //}

            // Check input content value
            if (string.IsNullOrEmpty(input))
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("Request content is empty", HttpStatusCode.Conflict));
            }

            // Convert the input string into InputClaimsModel object
            InputClaimsModel inputClaims = InputClaimsModel.Parse(input);

            if (inputClaims == null)
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("Can not deserialize input claims", HttpStatusCode.Conflict));
            }

            if (string.IsNullOrEmpty(inputClaims.signInName))
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("User 'signInName' is null or empty", HttpStatusCode.Conflict));
            }

            //if (string.IsNullOrEmpty(inputClaims.password))
            //{
            //    return Content(HttpStatusCode.Conflict, new B2CResponseModel("Password is null or empty", HttpStatusCode.Conflict));
            //}


            AzureADGraphClient azureADGraphClient = new AzureADGraphClient(ConfigurationManager.AppSettings["Tenant"],
                                                                            ConfigurationManager.AppSettings["ClientId"],
                                                                            ConfigurationManager.AppSettings["ClientSecret"]);

            GraphAccountModel account = azureADGraphClient.SearcUserBySignInNames(inputClaims.signInName).Result;

            // User already exists, no need to migrate.
            if (account != null)
            {
                return Ok();
            }

            B2CResponseModel outputClaimsCol = new B2CResponseModel("", HttpStatusCode.OK);
            Ldap.Controllers.ValuesController tmp = new Ldap.Controllers.ValuesController();
            outputClaimsCol.isMigrated = false;
            outputClaimsCol.email = inputClaims.signInName;

            //Only migrate account that is not migrated already, and verified successfully within the local LDAP store. 
            if (account == null && tmp.VerifyCredentials(inputClaims.signInName, inputClaims.password))
            {
                inputClaims.sn = "EID";
                inputClaims.givenName = inputClaims.signInName;
                inputClaims.email = string.Format("{0}@noreply.com", inputClaims.signInName);
               
                bool result = MigrateUser(azureADGraphClient, inputClaims);
                if (result)
                {
                    outputClaimsCol.displayName = inputClaims.sn;
                    outputClaimsCol.givenName = inputClaims.givenName;
                    outputClaimsCol.surName = inputClaims.email;
                    outputClaimsCol.password = inputClaims.password;
                    outputClaimsCol.isMigrated = true;

                }
            }
            return Ok(outputClaimsCol);

            //// Initiate the output claim object
            //B2CResponseModel outputClaims = new B2CResponseModel("", HttpStatusCode.OK);
            //outputClaims.newPassword = inputClaims.password;
            //outputClaims.email = inputClaims.signInName;
            //outputClaims.needToMigrate = "null";

            //Ldap.Controllers.ValuesController tmp = new Ldap.Controllers.ValuesController();
            //if (tmp.VerifyCredentials(inputClaims.signInName, inputClaims.password))
            //{
              
            //    outputClaims.givenName = "Test " + DateTime.UtcNow.ToLongTimeString();
            //    outputClaims.surName = "User " + DateTime.UtcNow.ToLongDateString();
            //    outputClaims.needToMigrate = "local";
            //}

            


            //outputClaims.displayName = userMigrationEntity.DisplayName;
            //outputClaims.surName = userMigrationEntity.LastName;
            //outputClaims.givenName = userMigrationEntity.FirstName;

            // Create a retrieve operation that takes a customer entity.
            // Note: Azure Blob Table query is case sensitive, always set the input email to lower case
            //var retrieveOperation = TableOperation.Retrieve<UserTableEntity>(Consts.MigrationTablePartition, inputClaims.signInName.ToLower());

            //CloudTable table = await GetSignUpTable(this.AppSettings.BlobStorageConnectionString);

            // Execute the retrieve operation.
            //TableResult tableEntity = await table.ExecuteAsync(retrieveOperation);

            //TableResult tableEntity = null;

            //if (tableEntity != null && tableEntity.Result != null)
            //{
            //    UserTableEntity userMigrationEntity = ((UserTableEntity)tableEntity.Result);
            //    try
            //    {
            //        outputClaims.needToMigrate = "local";

            //        // Compare the password entered by the user and the one in the migration table.
            //        // Don't compare in password reset flow (useInputPassword is true)
            //        if (inputClaims.useInputPassword || (inputClaims.password == userMigrationEntity.Password))
            //        {
            //            outputClaims.newPassword = inputClaims.password;
            //            outputClaims.email = inputClaims.signInName;
            //            outputClaims.displayName = userMigrationEntity.DisplayName;
            //            outputClaims.surName = userMigrationEntity.LastName;
            //            outputClaims.givenName = userMigrationEntity.FirstName;

            //            // Remove the user entity from migration table
            //            TableOperation deleteOperation = TableOperation.Delete((UserTableEntity)tableEntity.Result);
            //            //await table.ExecuteAsync(deleteOperation);
            //        }
            //        else
            //        {
            //            return Content(HttpStatusCode.Conflict, new B2CResponseModel("Your password is incorrect (migration API)", HttpStatusCode.Conflict));
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        return Content(HttpStatusCode.Conflict, new B2CResponseModel($"User migration error: {ex.Message}", HttpStatusCode.Conflict));
            //    }
            //}

            //return Ok(outputClaims);
        }

        [System.Web.Http.HttpPost]
        public IHttpActionResult LoalAccountSignIn()
        {
            try
            {
                return Migrate();
                //return Ok();
            }
            catch
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("Can not migrate user", HttpStatusCode.Conflict));

            }
        }

        public IHttpActionResult User()
        {
           return  ProcessRequest();
           
        }
        public IHttpActionResult Create()
        {
            return ProcessRequest();
        }

        private IHttpActionResult ProcessRequest()
        {
            string input = Request.Content.ReadAsStringAsync().Result;

            // If not data came in, then return
            if (this.Request.Content == null)
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("Request content is null", HttpStatusCode.Conflict));
            }

            //// Read the input claims from the request body
            //using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            //{
            //    input = await reader.ReadToEndAsync();
            //}

            // Check input content value
            if (string.IsNullOrEmpty(input))
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("Request content is empty", HttpStatusCode.Conflict));
            }

            // Convert the input string into InputClaimsModel object
            PeopleSoftInputClaimsModel inputClaims = PeopleSoftInputClaimsModel.Parse(input);

            if (inputClaims == null)
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("Can not deserialize input claims", HttpStatusCode.Conflict));
            }

            if (string.IsNullOrEmpty(inputClaims.uid))
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("User 'uid' is null or empty", HttpStatusCode.Conflict));
            }

            if (string.IsNullOrEmpty(inputClaims.password))
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("Password is null or empty", HttpStatusCode.Conflict));
            }

            //bool isEmail = Regex.IsMatch(emailString, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);

            if (string.IsNullOrEmpty(inputClaims.email) ||
               !Regex.IsMatch(inputClaims.email, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase))
            {
                return Content(HttpStatusCode.Conflict, new B2CResponseModel("Email is empty or not in the correct format", HttpStatusCode.Conflict));
            }

            AzureADGraphClient azureADGraphClient = new AzureADGraphClient(ConfigurationManager.AppSettings["Tenant"],
                                                                            ConfigurationManager.AppSettings["ClientId"],
                                                                            ConfigurationManager.AppSettings["ClientSecret"]);

            GraphAccountModel account = azureADGraphClient.SearcUserBySignInNames(inputClaims.uid).Result;
            B2CPeopleSoftResponseModel outputClaimsCol = new B2CPeopleSoftResponseModel("", HttpStatusCode.OK);
            Ldap.Controllers.ValuesController tmp = new Ldap.Controllers.ValuesController();
            outputClaimsCol.isMigrated = false;
            outputClaimsCol.username = inputClaims.uid;


            //Only migrate account that is not migrated already, and verified successfully within the local LDAP store. 
            if (account == null)
            {
                inputClaims.givenname = GetClaimValue(inputClaims.givenname);
                inputClaims.sn = GetClaimValue(inputClaims.sn);

                bool result = CreateUser(azureADGraphClient, inputClaims);
                if (result)
                {
                    outputClaimsCol.password = GetClaimValue(inputClaims.password);
                    outputClaimsCol.displayName = GetClaimValue(inputClaims.sn);
                    outputClaimsCol.email = inputClaims.email;
                    outputClaimsCol.givenName = inputClaims.givenname;
                    outputClaimsCol.surName = inputClaims.givenname;
                    outputClaimsCol.isMigrated = false;

                }
            }
            //Update user
            else
            {
                //TODO: Check for pasword as may want to stop update to it.
                inputClaims.givenname = inputClaims.givenname == null ? account.surname : inputClaims.givenname;
                inputClaims.sn = inputClaims.sn == null ? account.displayName : inputClaims.sn;
                inputClaims.email = inputClaims.email == null ? account.givenName : inputClaims.email;
                inputClaims.isActivated = inputClaims.isActivated == null ? account.accountEnabled : inputClaims.isActivated;


                bool result = UpdateUser(azureADGraphClient, inputClaims, account.objectId);
                if (result)
                {
                    outputClaimsCol.password = GetClaimValue(inputClaims.password);
                    outputClaimsCol.displayName = GetClaimValue(inputClaims.sn);
                    outputClaimsCol.email = inputClaims.email;
                    outputClaimsCol.givenName = inputClaims.givenname;
                    outputClaimsCol.surName = inputClaims.givenname;
                    outputClaimsCol.isActivated = (bool)inputClaims.isActivated;
                    //outputClaimsCol.isMigrated = false;

                }
                //return Content(HttpStatusCode.Conflict, new B2CResponseModel($"User already exists {inputClaims.uid}", HttpStatusCode.Conflict));

            }
            return Ok(outputClaimsCol);
        }

        private string GetClaimValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "n/a";
            return value;

        }
        private bool MigrateUser(AzureADGraphClient azureADGraphClient, 
                                        InputClaimsModel inputClaims)
        {
            //AzureADGraphClient azureADGraphClient = new AzureADGraphClient(this.AppSettings.Tenant, this.AppSettings.ClientId, this.AppSettings.ClientSecret);

            // Create the user using Graph API
            return azureADGraphClient.CreateAccount(
                "username",
                inputClaims.signInName,
                null,
                null,
                null,
                inputClaims.password,
                inputClaims.sn,
                inputClaims.email,
                inputClaims.givenName).Result;

        }

        private bool UpdateUser(AzureADGraphClient azureADGraphClient,
                                       PeopleSoftInputClaimsModel inputClaims, string objectId)
        {
            //AzureADGraphClient azureADGraphClient = new AzureADGraphClient(this.AppSettings.Tenant, this.AppSettings.ClientId, this.AppSettings.ClientSecret);

            // Create the user using Graph API
            return azureADGraphClient.UpdateAccount(
                objectId,
                "userName",
                inputClaims.uid,
                null,
                null,
                inputClaims.email,
                inputClaims.password,
                inputClaims.sn,
                inputClaims.email,
                inputClaims.givenname,
                (bool)inputClaims.isActivated).Result;

        }
        private bool CreateUser(AzureADGraphClient azureADGraphClient,
                                        PeopleSoftInputClaimsModel inputClaims)
        {
            //AzureADGraphClient azureADGraphClient = new AzureADGraphClient(this.AppSettings.Tenant, this.AppSettings.ClientId, this.AppSettings.ClientSecret);

            // Create the user using Graph API
            return azureADGraphClient.CreateAccount(
                "userName",
                inputClaims.uid,
                null,
                null,
                inputClaims.email,
                inputClaims.password,
                inputClaims.sn,
                inputClaims.email,
                inputClaims.givenname).Result;

        }
       
    
    }


    public class AzureADGraphClient
    {
        private AuthenticationContext authContext;
        private ClientCredential credential;
        static private AuthenticationResult AccessToken;

        public readonly string aadInstance = "https://login.microsoftonline.com/";
        public readonly string aadGraphResourceId = "https://graph.windows.net/";
        public readonly string aadGraphEndpoint = "https://graph.windows.net/";
        public readonly string aadGraphVersion = "api-version=1.6";

        public string Tenant { get; }
        public string ClientId { get; }
        public string ClientSecret { get; }

        public AzureADGraphClient(string tenant, string clientId, string clientSecret)
        {
            this.Tenant = tenant;
            this.ClientId = clientId;
            this.ClientSecret = clientSecret;

            // The AuthenticationContext is ADAL's primary class, in which you indicate the direcotry to use.
            this.authContext = new AuthenticationContext("https://login.microsoftonline.com/" + this.Tenant);

            // The ClientCredential is where you pass in your client_id and client_secret, which are 
            // provided to Azure AD in order to receive an access_token using the app's identity.
            this.credential = new ClientCredential(this.ClientId, this.ClientSecret);
        }

        /// <summary>
        /// Create consumer user accounts
        /// When creating user accounts in a B2C tenant, you can send an HTTP POST request to the /users endpoint
        /// </summary>
        /// userType must be userName or emailAddress
        public async Task<bool> CreateAccount(
                                            string userType,
                                            string signInName,
                                            string issuer,
                                            string issuerUserId,
                                            string email,
                                            string password,
                                            string displayName,
                                            string givenName,
                                            string surname)
        {
            if (string.IsNullOrEmpty(signInName) && string.IsNullOrEmpty(issuerUserId))
                throw new Exception("You must provide user's signInName or issuerUserId");

            if (string.IsNullOrEmpty(displayName) || displayName.Length < 1)
                throw new Exception("Dispay name is NULL or empty, you must provide valid dislay name");

            try
            {
                // Create Graph json string from object
                GraphAccountModel graphUserModel = new GraphAccountModel(
                                                Tenant,
                                                userType,
                                                signInName,
                                                issuer,
                                                issuerUserId,
                                                email,
                                                password,
                                                displayName,
                                                givenName,
                                                surname);

                // Send the json to Graph API end point
                await SendGraphRequest("/users/", null, graphUserModel.ToString(), HttpMethod.Post);

                Console.WriteLine($"Azure AD user account '{displayName}' created");

                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ObjectConflict"))
                {
                    if (ex.Message.Contains("signInNames "))
                        // TBD: Chnage to trace
                        Console.WriteLine($"User with same signInNames '{signInName}' already exists in Azure AD");
                    else if (ex.Message.Contains("userIdentities "))
                        // TBD: Chnage to trace
                        Console.WriteLine($"User with same userIdentities '{issuerUserId}' already exists in Azure AD");
                    else if (ex.Message.Contains("one or more"))
                        // TBD: Chnage to trace
                        Console.WriteLine($"User with same userIdentities '{issuerUserId}', and signInNames '{signInName}'  already exists in Azure AD");

                }

                return false;
            }
        }

        public async Task<bool> UpdateAccount(
                                           string objectId,
                                           string userType,
                                           string signInName,
                                           string issuer,
                                           string issuerUserId,
                                           string email,
                                           string password,
                                           string displayName,
                                           string givenName,
                                           string surname,
                                           bool isActivated)
        {
            if (string.IsNullOrEmpty(signInName) && string.IsNullOrEmpty(issuerUserId))
                throw new Exception("You must provide user's signInName or issuerUserId");

            if (string.IsNullOrEmpty(displayName) || displayName.Length < 1)
                throw new Exception("Dispay name is NULL or empty, you must provide valid dislay name");

            try
            {
                // Create Graph json string from object
                GraphAccountModel graphUserModel = new GraphAccountModel(
                                                Tenant,
                                                userType,
                                                signInName,
                                                issuer,
                                                issuerUserId,
                                                email,
                                                password,
                                                displayName,
                                                givenName,
                                                surname);

                graphUserModel.objectId = objectId;
                graphUserModel.accountEnabled = isActivated;


                // Send the json to Graph API end point
                await SendGraphRequest(string.Format("/users/{0}", graphUserModel.objectId), null, graphUserModel.ToString(), new HttpMethod("PATCH"));

                Console.WriteLine($"Azure AD user account '{displayName}' updated");

                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ObjectConflict"))
                {
                    if (ex.Message.Contains("signInNames "))
                        // TBD: Chnage to trace
                        Console.WriteLine($"User with same signInNames '{signInName}' already exists in Azure AD");
                    else if (ex.Message.Contains("userIdentities "))
                        // TBD: Chnage to trace
                        Console.WriteLine($"User with same userIdentities '{issuerUserId}' already exists in Azure AD");
                    else if (ex.Message.Contains("one or more"))
                        // TBD: Chnage to trace
                        Console.WriteLine($"User with same userIdentities '{issuerUserId}', and signInNames '{signInName}'  already exists in Azure AD");

                }

                return false;
            }
        }
        /// <summary>
        /// Search Azure AD user by signInNames property
        /// </summary>
        public async Task<GraphAccountModel> SearcUserBySignInNames(string signInNames)
        {
            string json = await SendGraphRequest("/users/",
                            $"$filter=signInNames/any(x:x/value eq '{signInNames}')",
                            null, HttpMethod.Get);

            GraphAccountsModel accounts = GraphAccountsModel.Parse(json);

            if (accounts.value != null && accounts.value.Count >= 1)
            {
                return accounts.value[0];
            }

            return null;
        }


        /// <summary>
        /// Handle Graph user API, support following HTTP methods: GET, POST and PATCH
        /// </summary>
        private async Task<string> SendGraphRequest(string api, string query, string data, HttpMethod method)
        {
            // Get the access toke to Graph API
            string acceeToken = await AcquireAccessToken();

            // Set the Graph url. Including: Graph-endpoint/tenat/users?api-version&query
            string url = $"{this.aadGraphEndpoint}{this.Tenant}{api}?{this.aadGraphVersion}";

            if (!string.IsNullOrEmpty(query))
            {
                url += "&" + query;
            }

            try
            {
                using (HttpClient http = new HttpClient())
                using (HttpRequestMessage request = new HttpRequestMessage(method, url))
                {
                    // Set the authorization header
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", acceeToken);

                    // For POST and PATCH set the request content 
                    if (!string.IsNullOrEmpty(data))
                    {
                        //Trace.WriteLine($"Graph API data: {data}");
                        request.Content = new StringContent(data, Encoding.UTF8, "application/json");
                    }

                    // Send the request to Graph API endpoint
                    using (HttpResponseMessage response =  http.SendAsync(request).Result)
                    {
                        string error = await response.Content.ReadAsStringAsync();

                        // Check the result for error
                        if (!response.IsSuccessStatusCode)
                        {
                            // Throw server busy error message
                            if (response.StatusCode == (HttpStatusCode)429)
                            {
                                // TBD: Add you error handling here
                            }

                            throw new Exception(error);
                        }

                        // Return the response body, usually in JSON format
                        return await response.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception)
            {
                // TBD: Add you error handling here
                throw;
            }
        }

        private async Task<string> AcquireAccessToken()
        {

            AzureADGraphClient.AccessToken = authContext.AcquireTokenAsync(this.aadGraphResourceId, credential).Result;
          
            // If the access token is null or about to be invalid, acquire new one
            //if (B2CGraphClient.AccessToken == null ||
            //    (B2CGraphClient.AccessToken.ExpiresOn.UtcDateTime > DateTime.UtcNow.AddMinutes(-10)))
            //{
            //    try
            //    {
            //        B2CGraphClient.AccessToken = await authContext.AcquireTokenAsync(this.aadGraphResourceId, credential);
            //    }
            //    catch (Exception ex)
            //    {
            //        // TBD: Add you error handling here
            //        throw;
            //    }
            //}

            return AzureADGraphClient.AccessToken.AccessToken;
        }

    }
}
