﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Graph;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ModernWorkplaceConcierge.TokenStorage;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Net.Http;
using ModernWorkplaceConcierge.Helpers;
using Newtonsoft.Json;
using IntuneConcierge.Helpers;
using System.Text;
using Newtonsoft.Json.Linq;
using System;

namespace ModernWorkplaceConcierge.Helpers
{
    public class GraphJson {

        [JsonProperty("@odata.type", NullValueHandling = NullValueHandling.Ignore)]
        public string OdataType { get; set; }
        [JsonProperty("@odata.context", NullValueHandling = NullValueHandling.Ignore)]
        public string OdataValue { get { return OdataType; } set { OdataType = value; } }

    }

    public static class GraphHelper
    {
        // Load configuration settings from PrivateSettings.config
        private static string appId = ConfigurationManager.AppSettings["ida:AppId"];
        private static string appSecret = ConfigurationManager.AppSettings["ida:AppSecret"];
        private static string redirectUri = ConfigurationManager.AppSettings["ida:RedirectUri"];
        private static string graphScopes = ConfigurationManager.AppSettings["ida:AppScopes"];
        private static string graphEndpoint = ConfigurationManager.AppSettings["ida:GraphEndpoint"];


        public static async Task<string> AddIntuneConfig(string result) {

            GraphJson json = JsonConvert.DeserializeObject<GraphJson>(result);

            if (json.OdataValue.Contains("CompliancePolicy"))
            {
                JObject o = JObject.Parse(result);

                JObject o2 = JObject.Parse(@"{scheduledActionsForRule:[{ruleName:'PasswordRequired',scheduledActionConfigurations:[{actionType:'block',gracePeriodHours:'0',notificationTemplateId:'',notificationMessageCCList:[]}]}]}");

                o.Add("scheduledActionsForRule", o2.SelectToken("scheduledActionsForRule"));

                string jsonPolicy = JsonConvert.SerializeObject(o);

                DeviceCompliancePolicy deviceCompliancePolicy = JsonConvert.DeserializeObject<DeviceCompliancePolicy>(jsonPolicy);

                var response = await GraphHelper.AddDeviceCompliancePolicyAsync(deviceCompliancePolicy);

                return response.ODataType + " | " +response.DisplayName;
            }
            else if (json.OdataValue.Contains("Configuration") && json.OdataValue.Contains("windows"))
            {
                DeviceConfiguration deviceConfiguration = JsonConvert.DeserializeObject<DeviceConfiguration>(result);

                // request fails when true :(
                deviceConfiguration.SupportsScopeTags = false;

                var response = await AddDeviceConfigurationAsync(deviceConfiguration);

                return response.ODataType + " | " + response.DisplayName;
            }
            else if (json.OdataValue.Contains("deviceManagementScripts"))
            {
                DeviceManagementScript deviceManagementScript = JsonConvert.DeserializeObject<DeviceManagementScript>(result);

                // remove id - otherwise request fails
                deviceManagementScript.Id = "";

                var response = await AddDeviceManagementScriptsAsync(deviceManagementScript);

                return "#microsoft.graph.deviceManagementScript" + " | " + response.DisplayName;
            }
            else if (json.OdataValue.Contains("WindowsAutopilotDeploymentProfile"))
            {
                WindowsAutopilotDeploymentProfile windowsAutopilotDeploymentProfile = JsonConvert.DeserializeObject<WindowsAutopilotDeploymentProfile>(result);

                var response = await AddWindowsAutopilotDeploymentProfile(windowsAutopilotDeploymentProfile);

                return response.ODataType + " | " + response.DisplayName;
            }
            else
            {
                return null;
            }
        }

        public static async Task<string> AddConditionalAccessPolicyAsync(string ConditionalAccessPolicyJSON)
        {
            var graphClient = GetAuthenticatedClient();

            string requestUrl = graphEndpoint + "/conditionalAccess/policies";

            HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(ConditionalAccessPolicyJSON, Encoding.UTF8, "application/json")
                            
            };

            // Authenticate (add access token) our HttpRequestMessage
            await graphClient.AuthenticationProvider.AuthenticateRequestAsync(hrm);

            // Send the request and get the response.
            HttpResponseMessage response = await graphClient.HttpProvider.SendAsync(hrm);

            return await response.Content.ReadAsStringAsync();
        }

        // Get's ESP, Enrollment restrictions, WHFB settings etc...
        public static async Task<IEnumerable<DeviceEnrollmentConfiguration>> GetDeviceEnrollmentConfigurationsAsync()
        {
            var graphClient = GetAuthenticatedClient();

            var deviceManagementScripts = await graphClient.DeviceManagement.DeviceEnrollmentConfigurations.Request().GetAsync();

            return deviceManagementScripts.CurrentPage;
        }

        public static async Task<IEnumerable<DeviceManagementScript>> GetDeviceManagementScriptsAsync()
        {
            var graphClient = GetAuthenticatedClient();

            var result = await graphClient.DeviceManagement.DeviceManagementScripts.Request().GetAsync();

            return result.CurrentPage;

        }

        public static async Task<DeviceManagementScript> AddDeviceManagementScriptsAsync(DeviceManagementScript deviceManagementScript)
        {
            var graphClient = GetAuthenticatedClient();

            var response = await graphClient.DeviceManagement.DeviceManagementScripts.Request().AddAsync(deviceManagementScript);

            return response;
        }

        public static async Task<DeviceManagementScript> GetDeviceManagementScriptsAsync(string Id)
        {
            var graphClient = GetAuthenticatedClient();

            DeviceManagementScript deviceManagementScript = await graphClient.DeviceManagement.DeviceManagementScripts[Id].Request().GetAsync();

            return deviceManagementScript;
        }

        public static async Task<string> GetDeviceManagementScriptRawAsync(string Id)
        {
            var graphClient = GetAuthenticatedClient();

            string requestUrl = graphEndpoint + "/deviceManagement/deviceManagementScripts/"+Id;

            HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            // Authenticate (add access token) our HttpRequestMessage
            await graphClient.AuthenticationProvider.AuthenticateRequestAsync(hrm);

            // Send the request and get the response.
            HttpResponseMessage response = await graphClient.HttpProvider.SendAsync(hrm);

            string result = await response.Content.ReadAsStringAsync(); //right!

            return result;
        }

        public static async Task<string> GetConditionalAccessPoliciesAsync()
        {
            var graphClient = GetAuthenticatedClient();

            string requestUrl = graphEndpoint + "/conditionalAccess/policies";

            HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            
            // Authenticate (add access token) our HttpRequestMessage
            await graphClient.AuthenticationProvider.AuthenticateRequestAsync(hrm);

            // Send the request and get the response.
            HttpResponseMessage response = await graphClient.HttpProvider.SendAsync(hrm);

            string result = await response.Content.ReadAsStringAsync(); //right!

            return result;
        }

        public static async Task<string> GetConditionalAccessPolicyAsync(string Id)
        {
            var graphClient = GetAuthenticatedClient();
            graphClient.BaseUrl = graphEndpoint;

            string requestUrl = graphEndpoint + "/conditionalAccess/policies/" + Id;

            HttpRequestMessage hrm = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            // Authenticate (add access token) our HttpRequestMessage
            await graphClient.AuthenticationProvider.AuthenticateRequestAsync(hrm);

            // Send the request and get the response.
            HttpResponseMessage response = await graphClient.HttpProvider.SendAsync(hrm);

            string result = await response.Content.ReadAsStringAsync(); 

            return result;
        }

        public static async Task<IEnumerable<DeviceConfiguration>> GetDeviceConfigurationsAsync()
        {
            var graphClient = GetAuthenticatedClient();

            var deviceConfigurations = await graphClient.DeviceManagement.DeviceConfigurations.Request().GetAsync();
                
            return deviceConfigurations.CurrentPage;
        }

        public static async Task<DeviceConfiguration> AddDeviceConfigurationAsync(DeviceConfiguration deviceConfiguration)
        {
            var graphClient = GetAuthenticatedClient();

            var result = await graphClient.DeviceManagement.DeviceConfigurations.Request().AddAsync(deviceConfiguration);

            return result;
        }

        public static async Task<IEnumerable<DeviceCompliancePolicy>> GetDeviceCompliancePoliciesAsync()
        {
            var graphClient = GetAuthenticatedClient();

            var deviceCompliancePolicies = await graphClient.DeviceManagement.DeviceCompliancePolicies.Request().GetAsync();

            return deviceCompliancePolicies.CurrentPage;
        }

        public static async Task <DeviceCompliancePolicy> AddDeviceCompliancePolicyAsync(DeviceCompliancePolicy deviceCompliancePolicy)
        {
            var graphClient = GetAuthenticatedClient();

            var result = await graphClient.DeviceManagement.DeviceCompliancePolicies.Request().AddAsync(deviceCompliancePolicy);

            return result;
        }


        public static async Task<IEnumerable<ManagedAppPolicy>> GetManagedAppProtectionAsync()
        {
            var graphClient = GetAuthenticatedClient();

            var managedAppProtection = await graphClient.DeviceAppManagement.ManagedAppPolicies.Request().GetAsync();

            return managedAppProtection.CurrentPage;
        }

        public static async Task<string> GetManagedAppProtectionAssignmentAsync(string Id)
        {
            var graphClient = GetAuthenticatedClient();

            var managedAppProtection = await graphClient.DeviceAppManagement.ManagedAppPolicies[Id].Request().GetAsync();

            string result = JsonConvert.SerializeObject(managedAppProtection, Formatting.Indented);

            result.Insert(0,"{\"apps\":");

            result.Insert(result.Length - 1, "}");

            return result;
        }

        public static async Task<ManagedAppPolicy> GetManagedAppProtectionAsync(string Id)
        {
            var graphClient = GetAuthenticatedClient();

            var managedAppProtection = await graphClient.DeviceAppManagement.IosManagedAppProtections[Id].Request().GetAsync();

            return managedAppProtection;
        }

        public static async Task <IEnumerable<WindowsAutopilotDeploymentProfile>> GetWindowsAutopilotDeploymentProfiles()
        {
            var graphClient = GetAuthenticatedClient();

            var windowsAutopilotDeploymentProfiles = await graphClient.DeviceManagement.WindowsAutopilotDeploymentProfiles.Request().GetAsync();

            return windowsAutopilotDeploymentProfiles.CurrentPage;
        }

        public static async Task<WindowsAutopilotDeploymentProfile> GetWindowsAutopilotDeploymentProfiles(string Id)
        {
            var graphClient = GetAuthenticatedClient();

            WindowsAutopilotDeploymentProfile windowsAutopilotDeploymentProfile = await graphClient.DeviceManagement.WindowsAutopilotDeploymentProfiles[Id].Request().GetAsync();

            return windowsAutopilotDeploymentProfile;
        }

        public static async Task<WindowsAutopilotDeploymentProfile> AddWindowsAutopilotDeploymentProfile(WindowsAutopilotDeploymentProfile autopilotDeploymentProfile)
        {
            var graphClient = GetAuthenticatedClient();

            var response = await graphClient.DeviceManagement.WindowsAutopilotDeploymentProfiles.Request().AddAsync(autopilotDeploymentProfile);

            return response;
        }

        public static async Task<Organization> GetOrgDetailsAsync()
        {
            var graphClient = GetAuthenticatedClient();
               
            var org =  await graphClient.Organization.Request().GetAsync();

            Organization organization = org.CurrentPage.First();

            return organization;
        }

        public static async Task<string> GetDefaultDomain()
        {
            Organization organization = await GetOrgDetailsAsync();

            string verifiedDomain = organization.VerifiedDomains.First().Name;

            foreach (VerifiedDomain domain in organization.VerifiedDomains)
            {
                if ((bool)domain.IsDefault)
                {
                    verifiedDomain = domain.Name;
                }

            }

            return verifiedDomain;
        }

        public static async Task<User> GetUserDetailsAsync(string accessToken)
        {
            var graphClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                        requestMessage.Headers.Authorization =
                            new AuthenticationHeaderValue("Bearer", accessToken);
                    }));

             return await graphClient.Me.Request().GetAsync();
        }

        private static GraphServiceClient GetAuthenticatedClient()
        {
            return new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                        var idClient = ConfidentialClientApplicationBuilder.Create(appId)
                            .WithRedirectUri(redirectUri)
                            .WithClientSecret(appSecret)
                            .Build();

                        var tokenStore = new SessionTokenStore(idClient.UserTokenCache, 
                            HttpContext.Current, ClaimsPrincipal.Current);

                        var accounts = await idClient.GetAccountsAsync();

                    // By calling this here, the token can be refreshed
                    // if it's expired right before the Graph call is made
                    var scopes = graphScopes.Split(' ');
                        var result = await idClient.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                            .ExecuteAsync();

                        requestMessage.Headers.Authorization =
                            new AuthenticationHeaderValue("Bearer", result.AccessToken);
                    }));
        }
    }
}