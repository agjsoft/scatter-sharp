﻿using EosSharp;
using Newtonsoft.Json.Linq;
using ScatterSharp.Api;
using ScatterSharp.Providers;
using ScatterSharp.Storage;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace ScatterSharp
{
    public class Scatter : IDisposable
    {
        private readonly string WSURI = "{0}://{1}:{2}/socket.io/?EIO=3&transport=websocket";
        private SocketService SocketService { get; set; } 
               
        public string AppName { get; set; }
        public Network Network { get; set; }
        public Identity Identity { get; set; }

        public class Blockchains
        {
            public static readonly string EOSIO = "eos";
            public static readonly string ETH = "eth";
            public static readonly string TRX = "trx";
        };

        public Scatter(string appName, Network network, IAppStorageProvider storageProvider = null)
        {
            SocketService = new SocketService(storageProvider ?? new MemoryStorageProvider(), appName);
            AppName = appName;
            Network = network;
        }

        public void Dispose()
        {
            SocketService.Dispose();
        }

        public async Task Connect(CancellationToken? cancellationToken = null)
        {
            try
            {
                //Try connect with wss connection
                Uri wssURI = new Uri(string.Format(WSURI, "wss", "local.get-scatter.com", "50006"));
                await SocketService.Link(wssURI, cancellationToken);
            }
            catch(WebSocketException)
            {
                //try normal ws connection
                SocketService = new SocketService(new MemoryStorageProvider(), AppName);
                Uri wsURI = new Uri(string.Format(WSURI, "ws", "127.0.0.1", "50005"));
                await SocketService.Link(wsURI, cancellationToken);
            }

            this.Identity = await this.GetIdentityFromPermissions();
        }

        public Eos Eos()
        {
            if (!SocketService.IsConnected())
                throw new Exception("Scatter is not connected.");

            if (Network == null)
                throw new ArgumentNullException("network");

            string httpEndpoint = "";

            if (Network.Port == 443)
                httpEndpoint += "https://" + Network.Host;
            else
                httpEndpoint += "http://" + Network.Host + ":" + Network.Port;

            return new Eos(new EosConfigurator()
            {
                ChainId = Network.ChainId,
                HttpEndpoint = httpEndpoint,
                SignProvider = new ScatterSignatureProvider(this)
            });
        }

        public async Task<string> GetVersion()
        {
            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "getVersion",
                Payload = new { origin = AppName }
            });

            ThrowOnApiError(result);

            return result.ToObject<string>();
        }

        public async Task<Identity> GetIdentity(IdentityRequiredFields requiredFields)
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "getOrRequestIdentity",
                Payload = new { fields = requiredFields, origin = AppName }
            });

            ThrowOnApiError(result);
            
            Identity = result.ToObject<Identity>();

            return Identity;
        }

        public async Task<Identity> GetIdentityFromPermissions()
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "identityFromPermissions",
                Payload = new { origin = AppName }
            });

            ThrowOnApiError(result);

            if(result.Type == JTokenType.Object)
                Identity = result.ToObject<Identity>();

            return Identity;
        }

        public async Task<bool> ForgetIdentity()
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "forgetIdentity",
                Payload = new { origin = AppName }
            });

            ThrowOnApiError(result);

            Identity = null;
            return result.ToObject<bool>();
        }

        public async Task<string> Authenticate(string nonce)
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "authenticate",
                Payload = new { nonce, origin = AppName }
            });

            ThrowOnApiError(result);

            return result.ToObject<string>();
        }

        public async Task<string> GetArbitrarySignature(string publicKey, string data, string whatfor = "", bool isHash = false)
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "requestArbitrarySignature",
                Payload = new { publicKey, data, whatfor, isHash, origin = AppName }
            });

            ThrowOnApiError(result);

            return result.ToObject<string>();
        }

        public async Task<string> GetPublicKey(string blockchain)
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "getPublicKey",
                Payload = new { blockchain, origin = AppName }
            });

            ThrowOnApiError(result);

            return result.ToObject<string>();
        }

        public async Task<bool> LinkAccount(string publicKey)
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "linkAccount",
                Payload = new { publicKey, network = Network, origin = AppName }
            });

            ThrowOnApiError(result);

            return result.ToObject<bool>();
        }

        public async Task<bool> HasAccountFor()
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "hasAccountFor",
                Payload = new { network = Network, origin = AppName }
            });

            ThrowOnApiError(result);

            return result.ToObject<bool>();
        }

        public async Task<bool> SuggestNetwork()
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "requestAddNetwork",
                Payload = new { network = Network, origin = AppName }
            });

            ThrowOnApiError(result);

            return result.ToObject<bool>();
        }

        //TODO check
        public async Task<object> RequestTransfer(string to, string amount, object options = null)
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "requestTransfer",
                Payload = new { network = Network, to, amount, options, origin = AppName }
            });

            ThrowOnApiError(result);

            return result;
        }

        public async Task<SignaturesResult> RequestSignature(object payload)
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "requestSignature",
                Payload = payload
            });

            ThrowOnApiError(result);

            return result.ToObject<SignaturesResult>();
        }

        //TODO test on new branch
        public async Task<string> GetEncryptionKey(string fromPublicKey, string toPublicKey, UInt64 nonce)
        {
            ThrowNoAuth();

            var result = await SocketService.SendApiRequest(new Request()
            {
                Type = "getEncryptionKey",
                Payload = new
                {
                    fromPublicKey,
                    toPublicKey,
                    nonce,
                    origin = AppName
                }
            });

            ThrowOnApiError(result);

            return result.ToObject<string>();
        }

        #region Utils
        private void ThrowNoAuth()
        {
            if (!SocketService.IsConnected())
                throw new Exception("Connect and Authenticate first - scatter.connect( appName )");
        }

        private static void ThrowOnApiError(JToken result)
        {
            if (result.Type != JTokenType.Object ||
               result.SelectToken("isError") == null)
                return;

            var apiError = result.ToObject<ApiError>();

            if (apiError != null)
                throw new Exception(apiError.Message);
        }
        #endregion
    }

}
