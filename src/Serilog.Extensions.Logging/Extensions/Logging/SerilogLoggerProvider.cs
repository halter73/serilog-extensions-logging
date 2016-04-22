// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
#if NET451
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
#endif
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace Serilog.Extensions.Logging
{
    public class SerilogLoggerProvider : ILoggerProvider, ILogEventEnricher
    {
        public const string OriginalFormatPropertyName = "{OriginalFormat}";

        // May be null; if it is, Log.Logger will be lazily used
        readonly ILogger _logger;

        public SerilogLoggerProvider(ILogger logger = null)
        {
            if (logger != null)
                _logger = logger.ForContext(new[] { this });
        }

        public FrameworkLogger CreateLogger(string name)
        {
            return new SerilogLogger(this, _logger, name);
        }

        public IDisposable BeginScope<TState>(string name, TState state)
        {
            return new SerilogLoggerScope(this, name, state);
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            for (var scope = CurrentScope; scope != null; scope = scope.Parent)
            {
                var stateStructure = scope.State as IEnumerable<KeyValuePair<string, object>>;
                if (stateStructure != null)
                {
                    foreach (var keyValue in stateStructure)
                    {
                        if (keyValue.Key == OriginalFormatPropertyName && keyValue.Value is string)
                            continue;

                        var property = propertyFactory.CreateProperty(keyValue.Key, keyValue.Value);
                        logEvent.AddPropertyIfAbsent(property);
                    }
                }
            }
        }

#if NET451
        private readonly string _currentScopeKey = nameof(SerilogLoggerScope) + "#" + Guid.NewGuid().ToString("n");

        public SerilogLoggerScope CurrentScope
        {
            get
            {
                var objectHandle = CallContext.LogicalGetData(_currentScopeKey) as ObjectHandle;
                return objectHandle?.Unwrap() as SerilogLoggerScope;
            }
            set
            {
                CallContext.LogicalSetData(_currentScopeKey, new ObjectHandle(value));
            }
        }
#else
        private System.Threading.AsyncLocal<SerilogLoggerScope> _value = new System.Threading.AsyncLocal<SerilogLoggerScope>();
        public SerilogLoggerScope CurrentScope
        {
            get
            {
                return _value.Value;
            }
            set
            {
                _value.Value = value;
            }
        }
#endif

        public void Dispose() { }
    }
}
