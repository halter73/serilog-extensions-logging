// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using Serilog.Core;
using Serilog.Events;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;
using System.Reflection;
using Serilog.Parsing;

namespace Serilog.Extensions.Logging
{
    public class SerilogLogger : FrameworkLogger
    {
        readonly SerilogLoggerProvider _provider;
        readonly string _name;
        readonly ILogger _logger;

        static readonly MessageTemplateParser _messageTemplateParser = new MessageTemplateParser();

        public SerilogLogger(
            SerilogLoggerProvider provider,
            ILogger logger = null,
            string name = null)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _provider = provider;
            _name = name;
            _logger = logger;

            // If a logger was passed, the provider has already added itself as an enricher
            _logger = _logger ?? Serilog.Log.Logger.ForContext(new[] { provider });

            if (_name != null)
            {
                _logger = _logger.ForContext(Constants.SourceContextPropertyName, name);
            }
        }

        public IDisposable BeginScopeImpl(object state)
        {
            return _provider.BeginScopeImpl(_name, state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _logger.IsEnabled(ConvertLevel(logLevel));
        }

        public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            var level = ConvertLevel(logLevel);
            if (!_logger.IsEnabled(level))
            {
                return;
            }

            var logger = _logger;
            string messageTemplate = null;
            var format = formatter ?? ((s,_) => LogFormatter.Formatter(s, null));

            var structure = state as ILogValues;
            if (structure != null)
            {
                foreach (var property in structure.GetValues())
                {
                    if (property.Key == SerilogLoggerProvider.OriginalFormatPropertyName && property.Value is string)
                    {
                        messageTemplate = (string)property.Value;
                    }
                    else if (property.Key.StartsWith("@"))
                    {
                        logger = logger.ForContext(property.Key.Substring(1), property.Value, destructureObjects: true);
                    }
                    else
                    {
                        logger = logger.ForContext(property.Key, property.Value);
                    }                    
                }

                var stateType = state.GetType();
                var stateTypeInfo = stateType.GetTypeInfo();
                // Imperfect, but at least eliminates `1 names
                if (messageTemplate == null && !stateTypeInfo.IsGenericType)
                {
                    messageTemplate = "{" + stateType.Name + ":l}";
                    logger = logger.ForContext(stateType.Name, format(state, null));
                }
            }

            if (messageTemplate == null && state != null)
            {
                messageTemplate = "{State:l}";
                logger = logger.ForContext("State", format(state, null));
            }

            if (string.IsNullOrEmpty(messageTemplate))
            {
                return;
            }

            if (eventId != 0)
            {
                logger = logger.ForContext("EventId", eventId);
            }

            var parsedTemplate = _messageTemplateParser.Parse(messageTemplate);
            var evt = new LogEvent(DateTimeOffset.Now, level, exception, parsedTemplate, Enumerable.Empty<LogEventProperty>());
            logger.Write(evt);
        }

        private LogEventLevel ConvertLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    return LogEventLevel.Fatal;
                case LogLevel.Error:
                    return LogEventLevel.Error;
                case LogLevel.Warning:
                    return LogEventLevel.Warning;
                case LogLevel.Information:
                    return LogEventLevel.Information;
                case LogLevel.Verbose:
                    return LogEventLevel.Debug;
                default:
                    return LogEventLevel.Verbose;
            }
        }
    }
}