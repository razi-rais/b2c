using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using AADB2C.JITUserMigration.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace AADB2C.JITUserMigration.Controllers
{
    //[Route("api/[controller]/[action]")]
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
            B2CResponseModel outputClaimsCol = new B2CResponseModel("", HttpStatusCode.OK);
            Ldap.Controllers.ValuesController tmp = new Ldap.Controllers.ValuesController();
            outputClaimsCol.isMigrated = false;
            outputClaimsCol.email = inputClaims.signInName;

            //Only migrate account that is not migrated already, and verified successfully within the local LDAP store. 
            if (account == null && tmp.VerifyCredentials(inputClaims.signInName, inputClaims.password))
            {
                bool result = MigrateUser(azureADGraphClient, inputClaims);
                if (result)
                {
                    outputClaimsCol.displayName = "This is a displayName from migrate app"; //TODO: read from LDAP
                    outputClaimsCol.givenName = "This is a givenName from migrate app"; //TODO: read from LDAP
                    outputClaimsCol.surName = "This is a surName from migrate app"; //TODO: read from LDAP
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

        private bool MigrateUser(AzureADGraphClient azureADGraphClient, 
                                        InputClaimsModel inputClaims)
        {
            //AzureADGraphClient azureADGraphClient = new AzureADGraphClient(this.AppSettings.Tenant, this.AppSettings.ClientId, this.AppSettings.ClientSecret);

            // Create the user using Graph API
            return azureADGraphClient.CreateAccount(
                "emailAddress",
                inputClaims.signInName,
                null,
                null,
                null,
                inputClaims.password,
                "This is a displayName from migrate app",
                "This is a first name from migrate app",
                "This is a last name from migrate app").Result;

        }

        //[System.Web.Http.HttpPost]
        //public IHttpActionResult RaiseErrorIfExists()
        //{
        //    string input = Request.Content.ReadAsStringAsync().Result;

        //    // If not data came in, then return
        //    if (this.Request.Content == null)
        //    {
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("Request content is null", HttpStatusCode.Conflict));
        //    }

        //    // Read the input claims from the request body
        //    //using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
        //    //{
        //    //    input = await reader.ReadToEndAsync();
        //    //}

        //    // Check input content value
        //    if (string.IsNullOrEmpty(input))
        //    {
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("Request content is empty", HttpStatusCode.Conflict));
        //    }

        //    // Convert the input string into InputClaimsModel object
        //    InputClaimsModel inputClaims = InputClaimsModel.Parse(input);

        //    if (inputClaims == null)
        //    {
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("Can not deserialize input claims", HttpStatusCode.Conflict));
        //    }

        //    if (string.IsNullOrEmpty(inputClaims.signInName))
        //    {
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("User 'signInName' is null or empty", HttpStatusCode.Conflict));
        //    }

        //    // Create a retrieve operation that takes a customer entity.
        //    // Note: Azure Blob Table query is case sensitive, always set the input email to lower case
        //    var retrieveOperation = TableOperation.Retrieve<UserTableEntity>(Consts.MigrationTablePartition, inputClaims.signInName.ToLower());

        //    //CloudTable table = await GetSignUpTable(this.AppSettings.BlobStorageConnectionString);

        //    // Execute the retrieve operation.
        //   // TableResult userMigrationEntity = await table.ExecuteAsync(retrieveOperation);
        //    TableResult userMigrationEntity = null;

        //    if (userMigrationEntity != null && userMigrationEntity.Result != null)
        //    {
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("A user with the specified ID already exists. Please choose a different one. (migration API)", HttpStatusCode.Conflict));
        //    }

        //    return Ok();
        //}

        //[HttpPost(Name = "RaiseErrorIfNotExists")]
        //public async Task<ActionResult> RaiseErrorIfNotExists()
        //{
        //    string input = null;

        //    // If not data came in, then return
        //    if (this.Request.Body == null)
        //    {
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("Request content is null", HttpStatusCode.Conflict));
        //    }

        //    // Read the input claims from the request body
        //    using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
        //    {
        //        input = await reader.ReadToEndAsync();
        //    }

        //    // Check input content value
        //    if (string.IsNullOrEmpty(input))
        //    {
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("Request content is empty", HttpStatusCode.Conflict));
        //    }

        //    // Convert the input string into InputClaimsModel object
        //    InputClaimsModel inputClaims = InputClaimsModel.Parse(input);

        //    if (inputClaims == null)
        //    {
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("Can not deserialize input claims", HttpStatusCode.Conflict));
        //    }

        //    if (string.IsNullOrEmpty(inputClaims.signInName))
        //    {
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("User 'signInName' is null or empty", HttpStatusCode.Conflict));
        //    }

        //    // Create a retrieve operation that takes a customer entity.
        //    // Note: Azure Blob Table query is case sensitive, always set the input email to lower case
        //    var retrieveOperation = TableOperation.Retrieve<UserTableEntity>(Consts.MigrationTablePartition, inputClaims.signInName.ToLower());

        //    CloudTable table = await GetSignUpTable(this.AppSettings.BlobStorageConnectionString);

        //    // Execute the retrieve operation.
        //    TableResult userMigrationEntity = await table.ExecuteAsync(retrieveOperation);

        //    // Checks if user exists in Azure AD B2C or the migration table. If not, raises an error
        //    if ((userMigrationEntity != null && userMigrationEntity.Result != null) || (string.IsNullOrEmpty(inputClaims.objectId) == false))
        //    {
        //        return Ok();
        //    }
        //    else
        //    { 
        //        return Content(HttpStatusCode.Conflict, new B2CResponseModel("An account could not be found for the provided user ID. (migration API)", HttpStatusCode.Conflict));
        //    }
        //}


        //public static async Task<CloudTable> GetSignUpTable(string conectionString)
        //{
        //    // Retrieve the storage account from the connection string.
        //    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(conectionString);

        //    // Create the table client.
        //    CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

        //    // Create the CloudTable object that represents the "people" table.
        //    CloudTable table = tableClient.GetTableReference(Consts.MigrationTable);

        //    // Create the table if it doesn't exist.
        //    await table.CreateIfNotExistsAsync();

        //    return table;
        //}
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
