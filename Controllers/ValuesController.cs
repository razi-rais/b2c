using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace Ldap.Controllers
{
    //[RoutePrefix("api/values")]
    public class ValuesController : ApiController
    {
        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get(string userName)
        {
            //string domainNamePath = GetCurrentDomainPath();
            //return new string[] { domainNamePath, "value2" };

            //return GetAllUsers();
            return GetAUser(userName);

        }

        [Route("api/user")]
        [HttpGet]
         public IEnumerable<string> GetUser(string name)
        {
            return GetAUser(name);
        }

        [Route("api/verifyusercred")]
        [HttpGet]
        public bool VerifyCredentials(string name, string password)
        {
            string domainName = "rbinrais";
            return AuthenticateUser(domainName, name, password);
        }
        public bool AuthenticateUser(string domainName, string userName, string password)
        {
            bool ret = false;

            try
            {
                DirectoryEntry de = new DirectoryEntry("LDAP://" + domainName,
                                    userName, password);
                DirectorySearcher dsearch = new DirectorySearcher(de);
                SearchResult results = null;

                results = dsearch.FindOne();

                ret = true;
            }
            catch
            {
                ret = false;
            }

            return ret;
        }
        public IEnumerable<string> Get()
        {
            return new string[] { "test", "value2" };
        }


            // GET api/values/5

            //public string GetUser(string userName)
            //{
            //    return "value";
            //}


            public string DomainPath()
        {
            return "test";
        }
        private string[] GetAllUsers()
        {
            SearchResultCollection results;
            DirectorySearcher ds = null;
            DirectoryEntry de = new DirectoryEntry(GetCurrentDomainPath());

            ds = new DirectorySearcher(de);
            ds.Filter = "(&(objectCategory=User)(objectClass=person))";

            results = ds.FindAll();
            string[] users = new string[results.Count];
            int count = 0;
            foreach (SearchResult sr in results)
            {
                //Debug.WriteLine(sr.Properties["name"][0].ToString());
                users[count++] = sr.Properties["name"][0].ToString();
                // The following is NOT available
                // Debug.WriteLine(sr.Properties["mail"][0].ToString());
            }

            return users;
        }
        private string GetCurrentDomainPath()
        {
            DirectoryEntry de = new DirectoryEntry("LDAP://RootDSE");

            return "LDAP://" + de.Properties["defaultNamingContext"][0].ToString();
        }

        private string[] GetAUser(string userName)
        {
            DirectorySearcher ds = null;
            DirectoryEntry de = new
              DirectoryEntry(GetCurrentDomainPath());
            SearchResult sr;

            // Build User Searcher
            ds = BuildUserSearcher(de);

            // Set the filter to look for a specific user
            ds.Filter = "(&(objectCategory=User)(objectClass=person)(name=" + userName + "))";

            sr = ds.FindOne();
            StringBuilder userAttributes = new StringBuilder();
            if (sr != null)
            {

                userAttributes.Append(GetPropertyValue(sr, "name"));
                userAttributes.Append(GetPropertyValue(sr, "givenname"));
                userAttributes.Append(GetPropertyValue(sr, "sn"));
                userAttributes.Append(GetPropertyValue(sr, "userPrincipalName"));
                userAttributes.Append(GetPropertyValue(sr, "distinguishedName"));
            }

            return userAttributes.ToString().Split();
        }

        public static string GetPropertyValue(SearchResult sr, string propertyName)
        {
            string ret = string.Empty;

            if (sr.Properties[propertyName].Count > 0)
                ret = sr.Properties[propertyName][0].ToString();

            return ret;
        }

        private DirectorySearcher BuildUserSearcher(DirectoryEntry de)
        {
            DirectorySearcher ds = null;

            ds = new DirectorySearcher(de);
            // Full Name
            ds.PropertiesToLoad.Add("name");
            // Email Address
            ds.PropertiesToLoad.Add("mail");
            // First Name
            ds.PropertiesToLoad.Add("givenname");
            // Last Name (Surname)
            ds.PropertiesToLoad.Add("sn");
            // Login Name
            ds.PropertiesToLoad.Add("userPrincipalName");
            // Distinguished Name
            ds.PropertiesToLoad.Add("distinguishedName");

            return ds;
        }
        private void GetAdditionalUserInfo()
        {
            SearchResultCollection results;
            DirectorySearcher ds = null;
            DirectoryEntry de = new DirectoryEntry(GetCurrentDomainPath());

            ds = new DirectorySearcher(de);
            // Full Name
            ds.PropertiesToLoad.Add("name");
            // Email Address
            ds.PropertiesToLoad.Add("mail");
            // First Name
            ds.PropertiesToLoad.Add("givenname");
            // Last Name (Surname)
            ds.PropertiesToLoad.Add("sn");
            // Login Name
            ds.PropertiesToLoad.Add("userPrincipalName");
            // Distinguished Name
            ds.PropertiesToLoad.Add("distinguishedName");

            ds.Filter = "(&(objectCategory=User)(objectClass=person))";

            results = ds.FindAll();

            foreach (SearchResult sr in results)
            {
                //if (sr.Properties["name"].Count > 0)
                //    //Debug.WriteLine(sr.Properties["name"][0].ToString());
                //// If not filled in, then you will get an error
                //if (sr.Properties["mail"].Count > 0)
                //    //Debug.WriteLine(sr.Properties["mail"][0].ToString());
                //if (sr.Properties["givenname"].Count > 0)
                //    //Debug.WriteLine(sr.Properties["givenname"][0].ToString());
                //if (sr.Properties["sn"].Count > 0)
                //    //Debug.WriteLine(sr.Properties["sn"][0].ToString());
                //if (sr.Properties["userPrincipalName"].Count > 0)
                //    //Debug.WriteLine(sr.Properties["userPrincipalName"][0].ToString());
                //if (sr.Properties["distinguishedName"].Count > 0)
                //    //Debug.WriteLine(sr.Properties["distinguishedName"][0].ToString());
            }
        }

    
        // POST api/values
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        public void Delete(int id)
        {
        }
    }
}
