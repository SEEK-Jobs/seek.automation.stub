﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using seek.automation.stub.Helpers;
using Serilog;

namespace seek.automation.stub
{
    public interface IExtendable : IDisposable
    {
        IExtendable FilterOnProviderState(string providerState);
        IExtendable FilterOnDescription(string description);
        IExtendable ClearFilters();
    }

    [SuppressMessage("ReSharper", "UseStringInterpolation")]
    public class Stub : IExtendable
    {
        private readonly ILogger _logger;
        private bool _matchBody;
        private IWebServer _webServer;
        private string _pact;
        private readonly int _port;
        private string _providerState;
        private string _description;
        private int _echoStatus;

        private Stub(int port)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var logDirectory = Path.Combine(currentDirectory, "stub");
            Directory.CreateDirectory(logDirectory);

            _logger = new LoggerConfiguration().WriteTo.Console().WriteTo.RollingFile(
                    Path.Combine(logDirectory, "seek.automation.stub-{Date}.txt"),
                    retainedFileCountLimit: 5,
                    fileSizeLimitBytes: 10000000
                ).CreateLogger();

            _port = port;
            _webServer = new WebServer(_logger);
        }

        public static Stub Create(int port)
        {
            var stub = new Stub(port);

            return stub;
        }

        public IExtendable FromJson(string pact, bool matchBody = true)
        {
            _logger.Information("Loading the pact from the pact string...");

            _pact = pact;
            _matchBody = matchBody;

            ValidatePact(_pact);

            Simulate();

            return this;
        }

        public IExtendable FromFile(string pactFilePath, bool matchBody = true)
        {
            _logger.Information("Loading the pact from the pact file...");

            if (!File.Exists(pactFilePath)) throw new FileNotFoundException();

            _matchBody = matchBody;
            _pact = File.ReadAllText(pactFilePath);

            ValidatePact(_pact);

            Simulate();

            return this;
        }

        public IExtendable FromPactbroker(string pactBrokerUrl, bool matchBody = true)
        {
            _logger.Information("Load the pact file from the broker...");

            _matchBody = matchBody;
            _pact = Helper.GetPactViaBroker(pactBrokerUrl);

            ValidatePact(_pact);

            Simulate();

            return this;
        }

        public IDisposable Echo(int statusCode)
        {
            _logger.Information(string.Format("Start running the echo simulation for the specified pact on port {0}", _port));

            _echoStatus = statusCode;

            _webServer = new WebServer(_logger);
            _webServer.Simulate(EchoCallback, _port);

            return this;
        }
        
        private HttpResponseMessage PactCallback(int port, HttpListenerContext listenerContext)
        {
            _logger.Information("Pact simulation on port {0}...", port);

            var regRes = Helper.PactRegistration(_pact, listenerContext, _providerState, _description, _matchBody);

            return regRes;
        }

        private HttpResponseMessage EchoCallback(int port, HttpListenerContext listenerContext)
        {
            _logger.Information("Echo simulation on port {0}...", port);

            var payload = Helper.GetRequestPostData(listenerContext.Request);
            var response = new HttpResponseMessage
            {
                StatusCode = (HttpStatusCode)_echoStatus,
                Content = new StringContent(string.IsNullOrEmpty(payload) ? string.Empty : payload)
            };

            return response;
        }

        private void ValidatePact(string pactContent)
        {
            try
            {
                _logger.Information("Validate the pact file as JSON...");
                JToken.Parse(pactContent);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Failed to read the pact file. Exception {0}", ex.Message));
                throw new InvalidDataException("The pact file is not a valid JSON document.");
            }
        }

        private void Simulate()
        {
            _logger.Information(string.Format("Start running the simulation for the specified pact on port {0}", _port));

            _webServer = new WebServer(_logger);
            _webServer.Simulate(PactCallback, _port);
        }

        public void Dispose()
        {
            _webServer.Dispose();
        }

        public IExtendable FilterOnProviderState(string providerState)
        {
            _providerState = providerState;

            return this;
        }

        public IExtendable FilterOnDescription(string description)
        {
            _description = description;

            return this;
        }

        public IExtendable ClearFilters()
        {
            _providerState = string.Empty;
            _description = string.Empty;

            return this;
        }
    }
}
