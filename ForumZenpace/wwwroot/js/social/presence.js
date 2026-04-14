import { connection, state } from './state.js';

let heartbeatInterval = null;
const onlineUserIds = new Set();

export const setUserOnlineStatus = (userId, isOnline) => {
    if (isOnline) {
        onlineUserIds.add(userId);
    } else {
        onlineUserIds.delete(userId);
    }

    document.querySelectorAll(`[data-presence-user-id="${userId}"]`).forEach((dot) => {
        dot.classList.toggle('is-online', isOnline);
    });
};

export const refreshKnownPresence = (root = document) => {
    if (!(root instanceof Document) && !(root instanceof Element)) {
        return;
    }

    root.querySelectorAll('[data-presence-user-id]').forEach((dot) => {
        if (!(dot instanceof HTMLElement)) {
            return;
        }

        const userId = Number.parseInt(dot.dataset.presenceUserId || '', 10);
        dot.classList.toggle('is-online', Number.isInteger(userId) && onlineUserIds.has(userId));
    });
};

export const startHeartbeat = () => {
    if (heartbeatInterval) return;
    heartbeatInterval = window.setInterval(() => {
        if (state.realtimeReady && connection) {
            connection.invoke('Heartbeat').catch(() => {});
        }
    }, 20000); // every 20 seconds
};

export const stopHeartbeat = () => {
    if (heartbeatInterval) {
        window.clearInterval(heartbeatInterval);
        heartbeatInterval = null;
    }
};

export const handleReconnectPresence = () => {
    startHeartbeat();
    connection.invoke('GetOnlineUsers')
        .then((onlineIds) => {
            if (Array.isArray(onlineIds)) {
                onlineUserIds.clear();
                onlineIds.forEach((id) => {
                    const userId = Number.parseInt(`${id}`, 10);
                    if (Number.isInteger(userId) && userId > 0) {
                        onlineUserIds.add(userId);
                    }
                });
                refreshKnownPresence();
            }
        })
        .catch(() => {});
};

document.addEventListener('zenpace:presence-refresh', (event) => {
    const root = event instanceof CustomEvent ? event.detail?.root : null;
    refreshKnownPresence(root instanceof Element ? root : document);
});
