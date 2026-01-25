/*
 Photino Bridge - Replaces Wails Runtime
 This file provides Wails-compatible API calls using Photino's messaging
*/

// RPC system for calling C# backend
const pendingCalls = new Map();
let callId = 0;

// Register callback for receiving messages from C# (Photino uses this pattern)
if (!window._runtimeListenerAdded) {
    window._runtimeListenerAdded = true;

    const registerExternalListener = () => {
        try {
            if (!window.external || typeof window.external.receiveMessage !== 'function') {
                return false;
            }

            // Photino sends messages via window.external.receiveMessage callback
            window.external.receiveMessage((message) => {
                try {
                    const data = typeof message === 'string' ? JSON.parse(message) : message;

                    // Handle events from backend
                    if (data.type === 'progress' || data.type === 'event') {
                        const eventName = data.eventName || 'progress-update';
                        const eventData = data.data || data;
                        console.log('Dispatching event:', eventName, eventData);
                        window.dispatchEvent(new CustomEvent(eventName, { detail: eventData }));
                        return;
                    }

                    // Handle RPC responses
                    if (data.Id && pendingCalls.has(data.Id)) {
                        const { resolve, reject } = pendingCalls.get(data.Id);
                        pendingCalls.delete(data.Id);
                        if (data.Error) {
                            reject(new Error(data.Error));
                        } else {
                            resolve(data.Result);
                        }
                    }
                } catch (e) {
                    console.error('Error parsing message:', e);
                }
            });

            return true;
        } catch (e) {
            console.error('Failed to register external listener:', e);
            return false;
        }
    };

    if (!registerExternalListener()) {
        // Retry a few times in case the bridge is injected after script load (macOS WebKit)
        let attempts = 0;
        const interval = setInterval(() => {
            attempts += 1;
            if (registerExternalListener() || attempts >= 20) {
                clearInterval(interval);
            }
        }, 250);
    }
}

function callBackend(method, ...args) {
    return new Promise((resolve, reject) => {
        const id = `call_${++callId}`;
        pendingCalls.set(id, { resolve, reject });
        
        const message = JSON.stringify({
            Id: id,
            Method: method,
            Args: args
        });
        
        if (window.external?.sendMessage) {
            window.external.sendMessage(message);
        } else {
            console.warn('No native bridge available');
            reject(new Error('Native bridge not available'));
        }
    });
}

// Event listeners storage
const eventListeners = new Map();

export function EventsOnMultiple(eventName, callback, maxCallbacks) {
    if (!eventListeners.has(eventName)) {
        eventListeners.set(eventName, []);
    }
    eventListeners.get(eventName).push({ callback, maxCallbacks, callCount: 0 });
    
    const handler = (e) => {
        const listeners = eventListeners.get(eventName) || [];
        listeners.forEach((listener, index) => {
            if (listener.maxCallbacks === -1 || listener.callCount < listener.maxCallbacks) {
                listener.callback(e.detail);
                listener.callCount++;
                if (listener.maxCallbacks !== -1 && listener.callCount >= listener.maxCallbacks) {
                    listeners.splice(index, 1);
                }
            }
        });
    };
    
    window.addEventListener(eventName, handler);
    return () => window.removeEventListener(eventName, handler);
}

export function EventsOn(eventName, callback) {
    return EventsOnMultiple(eventName, callback, -1);
}

export function EventsOff(eventName) {
    eventListeners.delete(eventName);
}

export function EventsOffAll() {
    eventListeners.clear();
}

export function EventsOnce(eventName, callback) {
    return EventsOnMultiple(eventName, callback, 1);
}

export function EventsEmit(eventName, ...args) {
    window.dispatchEvent(new CustomEvent(eventName, { detail: args }));
}

// Window functions
export function WindowMinimise() {
    callBackend('WindowMinimize');
}

export function WindowMaximise() {
    callBackend('WindowMaximize');
}

export function WindowToggleMaximise() {
    callBackend('WindowMaximize');
}

export function WindowUnmaximise() {
    callBackend('WindowMaximize');
}

export function WindowUnminimise() {
    callBackend('WindowRestore');
}

export function WindowFullscreen() {
    callBackend('WindowFullscreen');
}

export function WindowUnfullscreen() {
    callBackend('WindowUnfullscreen');
}

export function WindowIsFullscreen() {
    return callBackend('WindowIsFullscreen');
}

export function WindowIsMaximised() {
    return callBackend('WindowIsMaximized');
}

export function WindowIsMinimised() {
    return callBackend('WindowIsMinimized');
}

export function WindowIsNormal() {
    return callBackend('WindowIsNormal');
}

export function WindowCenter() {
    callBackend('WindowCenter');
}

export function WindowSetTitle(title) {
    callBackend('WindowSetTitle', title);
}

export function WindowSetSize(width, height) {
    callBackend('WindowSetSize', width, height);
}

export function WindowSetMinSize(width, height) {
    callBackend('WindowSetMinSize', width, height);
}

export function WindowSetMaxSize(width, height) {
    callBackend('WindowSetMaxSize', width, height);
}

export function WindowSetPosition(x, y) {
    callBackend('WindowSetPosition', x, y);
}

export function WindowGetPosition() {
    return callBackend('WindowGetPosition');
}

export function WindowGetSize() {
    return callBackend('WindowGetSize');
}

export function WindowShow() {
    // Already visible
}

export function WindowHide() {
    callBackend('WindowHide');
}

export function WindowReload() {
    window.location.reload();
}

export function WindowReloadApp() {
    window.location.reload();
}

export function WindowSetAlwaysOnTop(b) {
    callBackend('WindowSetAlwaysOnTop', b);
}

export function WindowSetBackgroundColour(R, G, B, A) {
    // Not directly supported
}

export function WindowSetSystemDefaultTheme() {}
export function WindowSetLightTheme() {}
export function WindowSetDarkTheme() {}

export function Quit() {
    callBackend('WindowClose');
}

export function Hide() {
    callBackend('WindowHide');
}

export function Show() {
    // Already visible
}

// Browser functions
export function BrowserOpenURL(url) {
    callBackend('BrowserOpenURL', url);
}

// Log functions (just log to console)
export function LogPrint(message) { console.log(message); }
export function LogTrace(message) { console.trace(message); }
export function LogDebug(message) { console.debug(message); }
export function LogInfo(message) { console.info(message); }
export function LogWarning(message) { console.warn(message); }
export function LogError(message) { console.error(message); }
export function LogFatal(message) { console.error('FATAL:', message); }

// Clipboard functions
export function ClipboardGetText() {
    return navigator.clipboard.readText();
}

export function ClipboardSetText(text) {
    return navigator.clipboard.writeText(text);
}

// Environment
export function Environment() {
    return callBackend('Environment');
}

// Screen functions
export function ScreenGetAll() {
    return Promise.resolve([{
        width: window.screen.width,
        height: window.screen.height,
        isPrimary: true
    }]);
}

// File drop (not supported in Photino)
export function OnFileDrop(callback, useDropTarget) {
    return () => {};
}

export function OnFileDropOff() {}

export function CanResolveFilePaths() {
    return false;
}

export function ResolveFilePaths(files) {
    return Promise.resolve([]);
}
