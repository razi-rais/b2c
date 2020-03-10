﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AADB2C.JITUserMigration.Models
{

    public class PeopleSoftInputClaimsModel
    {
        // Demo: User's object id in Azure AD B2C
        public string password { get; set; }
        public string givenname { get; set; }
        public string email { get; set; }
        public string sn { get; set; }
        public string uid { get; set; }
        public bool? isActivated { get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static PeopleSoftInputClaimsModel Parse(string JSON)
        {
            return JsonConvert.DeserializeObject(JSON, typeof(PeopleSoftInputClaimsModel)) as PeopleSoftInputClaimsModel;
        }
    }
    public class InputClaimsModel
    {
        // Demo: User's object id in Azure AD B2C
        public string signInName { get; set; }
        public string password { get; set; }
        public string objectId { get; set; }
        public bool useInputPassword { get; set; }
        public string givenName { get; set; }
        public string email { get; set; }
        public string sn { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static InputClaimsModel Parse(string JSON)
        {
            return JsonConvert.DeserializeObject(JSON, typeof(InputClaimsModel)) as InputClaimsModel;
        }
    }
}
