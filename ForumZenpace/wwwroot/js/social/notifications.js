import { 
    notificationLink, notificationBadge, notificationPage, notificationList,
    setNotificationBadge, setNotificationList, connection, isRealtimeAvailable,
    getCurrentReturnUrl
} from './state.js';
import { escapeHtml, getInitials, postSocialAction } from './utils.js';

export const ensureNotificationBadge = () => {
    if (notificationBadge instanceof HTMLElement) return notificationBadge;
    if (!(notificationLink instanceof HTMLElement)) return null;

    const badge = document.createElement('span');
    badge.className = 'notification-badge';
    badge.setAttribute('data-notification-badge', '');
    badge.hidden = true;
    notificationLink.appendChild(badge);
    setNotificationBadge(badge);
    return badge;
};

export const setUnreadCount = (count) => {
    const badge = ensureNotificationBadge();
    if (!(badge instanceof HTMLElement)) return;
    const nextCount = Number.isFinite(count) ? Math.max(0, count) : 0;
    badge.textContent = `${nextCount}`;
    badge.hidden = nextCount === 0;
};

export const ensureNotificationList = () => {
    if (notificationList instanceof HTMLElement) return notificationList;
    if (!(notificationPage instanceof HTMLElement)) return null;

    notificationPage.querySelector('.empty-state')?.remove();
    const surface = notificationPage.querySelector('.surface');
    if (!(surface instanceof HTMLElement)) return null;

    const list = document.createElement('div');
    list.className = 'notice-list';
    list.setAttribute('data-notification-list', '');
    surface.appendChild(list);
    setNotificationList(list);
    return list;
};

export const renderNotificationItem = (notification) => {
    const actorDisplayName = notification.actorDisplayName || 'Zenpace';
    const avatarMarkup = notification.actorAvatarUrl
        ? `<img src="${escapeHtml(notification.actorAvatarUrl)}" alt="${escapeHtml(actorDisplayName)}" />`
        : `<span>${escapeHtml(getInitials(actorDisplayName, 'Z'))}</span>`;
    const createdAt = notification.createdAt
        ? new Date(notification.createdAt).toLocaleString('vi-VN', { month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit' })
        : '';
        
    const tokenMeta = document.querySelector('meta[name="request-verification-token"]');
    const token = tokenMeta instanceof HTMLMetaElement ? tokenMeta.content : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
    const antiForgery = token ? `<input type="hidden" name="__RequestVerificationToken" value="${escapeHtml(token)}" />` : '';

    return `
        <div class="glass-card notice-item ${notification.isRead ? '' : 'notice-item--unread'}"
             data-notification-item
             data-notification-id="${notification.id}"
             data-friend-request-id="${notification.friendRequestId ?? ''}">
            <div class="notice-content">
                <div class="notice-head">
                    <div class="notice-avatar avatar-link">
                        ${avatarMarkup}
                        <span class="presence-dot" data-presence-user-id="${notification.actorUserId}"></span>
                    </div>
                    <div class="notice-copy">
                        <div class="notice-text">${escapeHtml(notification.content)}</div>
                        <div class="notice-meta">
                            <span><i class="fa-regular fa-clock"></i> ${escapeHtml(createdAt)}</span>
                            <span class="dot"></span>
                            <span data-notification-state>${notification.isRead ? 'Da doc' : 'Moi'}</span>
                            ${notification.canAcceptFriendRequest ? '<span class="dot"></span><span data-friend-request-state>Cho phan hoi</span>' : ''}
                        </div>
                    </div>
                </div>
            </div>
            <div class="notice-actions">
                ${notification.canAcceptFriendRequest && notification.friendRequestId ? `
                    <button type="button" class="btn btn-primary btn-sm" data-notification-accept data-friend-request-id="${notification.friendRequestId}">Chap nhan</button>
                    <button type="button" class="btn btn-text btn-sm" data-notification-decline data-friend-request-id="${notification.friendRequestId}">Tu choi</button>` : ''}
                ${notification.targetUrl ? `
                    <a href="${escapeHtml(notification.targetUrl)}" class="btn btn-outline btn-sm">${escapeHtml(notification.actionLabel || 'Mo')}</a>` : ''}
                ${!notification.isRead ? `
                    <form action="/Notification/MarkAsRead" method="post" data-mark-notification-read>
                        ${antiForgery}
                        <input type="hidden" name="id" value="${notification.id}" />
                        <button type="submit" class="notice-action" title="Danh dau da doc">
                            <i class="fa-solid fa-check"></i>
                        </button>
                    </form>` : ''}
            </div>
        </div>`;
};

export const prependNotification = (notification) => {
    const list = ensureNotificationList();
    if (!(list instanceof HTMLElement)) return;

    const existing = list.querySelector(`[data-notification-id="${notification.id}"]`);
    if (existing instanceof HTMLElement) {
        existing.outerHTML = renderNotificationItem(notification);
        const refreshed = list.querySelector(`[data-notification-id="${notification.id}"]`);
        if (refreshed instanceof HTMLElement) {
            document.dispatchEvent(new CustomEvent('zenpace:presence-refresh', {
                detail: { root: refreshed }
            }));
        }
        return;
    }
    list.insertAdjacentHTML('afterbegin', renderNotificationItem(notification));
    const inserted = list.querySelector(`[data-notification-id="${notification.id}"]`);
    if (inserted instanceof HTMLElement) {
        document.dispatchEvent(new CustomEvent('zenpace:presence-refresh', {
            detail: { root: inserted }
        }));
    }
};

export const updateNotificationResolution = (requestId, status) => {
    const item = notificationList?.querySelector(`[data-friend-request-id="${requestId}"]`);
    if (!(item instanceof HTMLElement)) return;

    item.classList.remove('notice-item--unread');
    item.querySelectorAll('[data-notification-accept], [data-notification-decline]').forEach((element) => element.remove());
    item.querySelectorAll('[data-mark-notification-read]').forEach((element) => element.remove());

    const stateDesc = item.querySelector('[data-notification-state]');
    if (stateDesc instanceof HTMLElement) {
        stateDesc.textContent = 'Da doc';
    }

    const friendRequestState = item.querySelector('[data-friend-request-state]');
    if (friendRequestState instanceof HTMLElement) {
        friendRequestState.textContent = status === 'Accepted' ? 'Da chap nhan' : 'Da tu choi';
    }
};

export const markNotificationAsReadLocally = (notificationId) => {
    const item = notificationList?.querySelector(`[data-notification-id="${notificationId}"]`);
    if (!(item instanceof HTMLElement)) return;

    item.classList.remove('notice-item--unread');
    item.querySelectorAll('[data-mark-notification-read]').forEach((element) => element.remove());

    const stateDesc = item.querySelector('[data-notification-state]');
    if (stateDesc instanceof HTMLElement) {
        stateDesc.textContent = 'Da doc';
    }
};

export const markAllNotificationsAsReadLocally = () => {
    notificationList?.querySelectorAll('[data-notification-item]').forEach((item) => {
        if (!(item instanceof HTMLElement)) {
            return;
        }

        item.classList.remove('notice-item--unread');
        item.querySelectorAll('[data-mark-notification-read]').forEach((element) => element.remove());

        const stateDesc = item.querySelector('[data-notification-state]');
        if (stateDesc instanceof HTMLElement) {
            stateDesc.textContent = 'Da doc';
        }
    });
};

export const bindNotificationPage = () => {
    notificationPage?.addEventListener('click', async (event) => {
        const acceptButton = event.target instanceof Element ? event.target.closest('[data-notification-accept]') : null;
        if (acceptButton instanceof HTMLButtonElement) {
            const requestId = Number.parseInt(acceptButton.dataset.friendRequestId || '', 10);
            if (Number.isInteger(requestId) && requestId > 0) {
                acceptButton.disabled = true;
                try {
                    if (isRealtimeAvailable()) {
                        await connection.invoke('AcceptFriendRequest', requestId);
                    } else {
                        const result = await postSocialAction('/Social/AcceptFriendRequest', {
                            requestId,
                            returnUrl: getCurrentReturnUrl()
                        });
                        updateNotificationResolution(result.requestId ?? requestId, result.status || 'Accepted');
                        setUnreadCount(Number.parseInt(`${result.unreadCount ?? 0}`, 10));
                    }
                } catch {
                    acceptButton.disabled = false;
                }
            }
            return;
        }

        const declineButton = event.target instanceof Element ? event.target.closest('[data-notification-decline]') : null;
        if (declineButton instanceof HTMLButtonElement) {
            const requestId = Number.parseInt(declineButton.dataset.friendRequestId || '', 10);
            if (Number.isInteger(requestId) && requestId > 0) {
                declineButton.disabled = true;
                try {
                    if (isRealtimeAvailable()) {
                        await connection.invoke('DeclineFriendRequest', requestId);
                    } else {
                        const result = await postSocialAction('/Social/DeclineFriendRequest', {
                            requestId,
                            returnUrl: getCurrentReturnUrl()
                        });
                        updateNotificationResolution(result.requestId ?? requestId, result.status || 'Declined');
                        setUnreadCount(Number.parseInt(`${result.unreadCount ?? 0}`, 10));
                    }
                } catch {
                    declineButton.disabled = false;
                }
            }
        }
    });

    notificationPage?.addEventListener('submit', async (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        if (form.hasAttribute('data-mark-notification-read')) {
            event.preventDefault();
            const idField = form.querySelector('input[name="id"]');
            const notificationId = Number.parseInt(idField instanceof HTMLInputElement ? idField.value : '', 10);
            if (!Number.isInteger(notificationId) || notificationId <= 0) {
                return;
            }

            const submitButton = form.querySelector('button[type="submit"]');
            if (submitButton instanceof HTMLButtonElement) {
                submitButton.disabled = true;
            }

            try {
                const result = await postSocialAction('/Notification/MarkAsRead', { id: notificationId });
                markNotificationAsReadLocally(result.id ?? notificationId);
                setUnreadCount(Number.parseInt(`${result.unreadCount ?? 0}`, 10));
            } catch {
                if (submitButton instanceof HTMLButtonElement) {
                    submitButton.disabled = false;
                }
            }

            return;
        }

        if (form.hasAttribute('data-mark-all-notifications-read')) {
            event.preventDefault();
            const submitButton = form.querySelector('button[type="submit"]');
            if (submitButton instanceof HTMLButtonElement) {
                submitButton.disabled = true;
            }

            try {
                const result = await postSocialAction('/Notification/MarkAllAsRead');
                markAllNotificationsAsReadLocally();
                setUnreadCount(Number.parseInt(`${result.unreadCount ?? 0}`, 10));
                form.remove();
            } catch {
                if (submitButton instanceof HTMLButtonElement) {
                    submitButton.disabled = false;
                }
            }
        }
    });
};
