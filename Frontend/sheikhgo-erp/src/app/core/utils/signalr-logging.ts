import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';

/** Strip JWT from SignalR log lines (WSS URLs often append ?access_token=...). */
const ACCESS_TOKEN_QUERY = /([?&]access_token=)[^&\s"']+/gi;

export function redactSignalRAccessToken(message: string): string {
  return message.replace(ACCESS_TOKEN_QUERY, '$1[REDACTED]');
}

/**
 * Prod: silent (None) so hub URLs never hit the console.
 * Non-prod: Information with token redaction for local debugging.
 */
export function signalRLogging(): signalR.LogLevel | signalR.ILogger {
  if (environment.production) {
    return signalR.LogLevel.None;
  }
  return new RedactingSignalRLogger(signalR.LogLevel.Information);
}

class RedactingSignalRLogger implements signalR.ILogger {
  constructor(private readonly minLevel: signalR.LogLevel) {}

  log(logLevel: signalR.LogLevel, message: string): void {
    if (logLevel < this.minLevel) {
      return;
    }
    const safe = redactSignalRAccessToken(message);
    switch (logLevel) {
      case signalR.LogLevel.Critical:
      case signalR.LogLevel.Error:
        console.error(safe);
        break;
      case signalR.LogLevel.Warning:
        console.warn(safe);
        break;
      case signalR.LogLevel.Information:
        console.info(safe);
        break;
      default:
        console.debug(safe);
        break;
    }
  }
}
