(() => {
    const body = document.body;
    const currentUserId = Number.parseInt(body.dataset.currentUserId || '', 10);
    const hubUrl = body.dataset.socialHubUrl || '';
    const signalRClient = window.signalR;

    if (!Number.isInteger(currentUserId) || currentUserId <= 0 || !hubUrl || !signalRClient) {
        return;
    }

    const connection = new signalRClient.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    const notificationLink = document.querySelector('[data-notification-link]');
    let notificationBadge = document.querySelector('[data-notification-badge]');
    const friendModal = document.querySelector('[data-friend-modal]');
    const friendList = document.querySelector('[data-friend-list]');
    const profileSocial = document.querySelector('[data-profile-social]');
    const profileSocialStatus = document.querySelector('[data-profile-social-status]');
    const notificationPage = document.querySelector('[data-notification-page]');
    let notificationList = document.querySelector('[data-notification-list]');
    let searchTimer = 0;
    let searchToken = 0;

    const escapeHtml = (value) => `${value ?? ''}`
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const getInitials = (displayName, fallback) => {
        const source = `${displayName || fallback || '?'}`.trim();
        const parts = source.split(/\s+/).filter(Boolean);
        if (parts.length <= 1) {
            return (parts[0] || '?').slice(0, 1).toUpperCase();
        }

        return `${parts[0].slice(0, 1)}${parts[parts.length - 1].slice(0, 1)}`.toUpperCase();
    };

    const getProfileTargetUserId = () => Number.parseInt(profileSocial?.dataset.targetUserId || '', 10);
    const getProfileTargetUsername = () => profileSocial?.dataset.targetUsername || '';

    const ensureNotificationBadge = () => {
        if (notificationBadge instanceof HTMLElement) {
            return notificationBadge;
        }

        if (!(notificationLink instanceof HTMLElement)) {
            return null;
        }

        notificationBadge = document.createElement('span');
        notificationBadge.className = 'notification-badge';
        notificationBadge.setAttribute('data-notification-badge', '');
        notificationBadge.hidden = true;
        notificationLink.appendChild(notificationBadge);
        return notificationBadge;
    };

    const setUnreadCount = (count) => {
        const badge = ensureNotificationBadge();
        if (!(badge instanceof HTMLElement)) {
            return;
        }

        const nextCount = Number.isFinite(count) ? Math.max(0, count) : 0;
        badge.textContent = `${nextCount}`;
        badge.hidden = nextCount === 0;
    };

    const setFriendSearchStatus = (message) => {
        const status = friendModal?.querySelector('[data-friend-search-status]');
        if (status instanceof HTMLElement) {
            status.textContent = message;
        }
    };

    const ensureFriendEmptyState = () => {
        if (!(friendList instanceof HTMLElement) || friendList.querySelector('[data-friend-card]')) {
            return;
        }

        friendList.innerHTML = `
            <div class="glass-panel social-card social-card--empty" data-friend-empty>
                <span class="social-card-icon"><i class="fa-regular fa-user"></i></span>
                <span class="social-card-copy">
                    <strong>Chua co ban</strong>
                    <span>Bat dau ket noi tu o Them ban</span>
                </span>
            </div>`;
    };

    const removeFriendEmptyState = () => friendList?.querySelector('[data-friend-empty]')?.remove();

    const getFriendCardState = (friend) => {
        if (friend.isMessageBlockedByViewer) {
            return {
                state: 'blocked-by-viewer',
                label: 'Ban dang chan tin nhan voi nguoi nay.',
                icon: 'fa-ban',
                hidden: false
            };
        }

        if (friend.isMessageBlockedByOtherUser) {
            return {
                state: 'blocked-by-other',
                label: 'Nguoi nay dang chan tin nhan voi ban.',
                icon: 'fa-shield-halved',
                hidden: false
            };
        }

        return {
            state: 'connected',
            label: `Trang ca nhan cua @@${friend.username}`,
            icon: 'fa-user-group',
            hidden: true
        };
    };

    const renderFriendCard = (friend) => {
        const friendState = getFriendCardState(friend);
        const avatarMarkup = friend.avatarUrl
            ? `<img src="${escapeHtml(friend.avatarUrl)}" alt="${escapeHtml(friend.displayName)}" />`
            : `<span>${escapeHtml(getInitials(friend.displayName, friend.username))}</span>`;

        return `
            <a href="/Profile/user/${encodeURIComponent(friend.username)}"
               class="glass-panel social-card social-card--friend"
               data-friend-card
               data-friend-user-id="${friend.userId}"
               data-friend-username="${escapeHtml(friend.username)}"
               data-friend-state="${friendState.state}"
               title="${escapeHtml(friendState.label)}">
                <span class="social-friend-badge${friendState.hidden ? ' is-hidden' : ''}" data-friend-status-badge aria-hidden="true">
                    <i class="fa-solid ${friendState.icon}"></i>
                </span>
                <span class="social-avatar">${avatarMarkup}</span>
                <span class="social-card-copy">
                    <strong>@${escapeHtml(friend.username)}</strong>
                </span>
            </a>`;
    };

    const upsertFriendCard = (friend) => {
        if (!(friendList instanceof HTMLElement)) {
            return;
        }

        removeFriendEmptyState();

        const existingCard = friendList.querySelector(`[data-friend-user-id="${friend.userId}"]`);
        if (existingCard instanceof HTMLElement) {
            existingCard.outerHTML = renderFriendCard(friend);
            return;
        }

        friendList.insertAdjacentHTML('afterbegin', renderFriendCard(friend));
    };

    const removeFriendCard = (friendUserId) => {
        friendList?.querySelector(`[data-friend-user-id="${friendUserId}"]`)?.remove();
        ensureFriendEmptyState();
    };

    const updateFriendCardBlockState = (targetUserId, payload) => {
        const card = friendList?.querySelector(`[data-friend-user-id="${targetUserId}"]`);
        if (!(card instanceof HTMLElement)) {
            return;
        }

        const badge = card.querySelector('[data-friend-status-badge]');
        const icon = badge?.querySelector('i');

        let state = 'connected';
        let label = `Trang ca nhan cua @@${card.dataset.friendUsername || ''}`;
        let iconClass = 'fa-user-group';

        if (payload.isMessageBlockedByViewer) {
            state = 'blocked-by-viewer';
            label = 'Ban dang chan tin nhan voi nguoi nay.';
            iconClass = 'fa-ban';
        } else if (payload.isMessageBlockedByOtherUser) {
            state = 'blocked-by-other';
            label = 'Nguoi nay dang chan tin nhan voi ban.';
            iconClass = 'fa-shield-halved';
        }

        card.dataset.friendState = state;
        card.title = label;

        if (badge instanceof HTMLElement) {
            badge.classList.toggle('is-hidden', state === 'connected');
        }

        if (icon instanceof HTMLElement) {
            icon.className = `fa-solid ${iconClass}`;
        }
    };

    const renderCandidateResults = (items) => {
        const container = friendModal?.querySelector('[data-friend-search-results]');
        if (!(container instanceof HTMLElement)) {
            return;
        }

        if (!Array.isArray(items) || items.length === 0) {
            container.innerHTML = `
                <div class="social-search-empty">
                    <i class="fa-solid fa-user-group"></i>
                    <span>Khong tim thay thanh vien phu hop.</span>
                </div>`;
            return;
        }

        container.innerHTML = items.map((item) => {
            const avatarMarkup = item.avatarUrl
                ? `<img src="${escapeHtml(item.avatarUrl)}" alt="${escapeHtml(item.displayName)}" />`
                : `<span>${escapeHtml(getInitials(item.displayName, item.username))}</span>`;

            return `
                <div class="social-result-card" data-candidate-user-id="${item.userId}" data-candidate-state="${escapeHtml(item.relationshipState)}">
                    <div class="social-result-avatar">${avatarMarkup}</div>
                    <div class="social-result-copy">
                        <strong>${escapeHtml(item.displayName)}</strong>
                        <span>@@${escapeHtml(item.username)}</span>
                        <small>${escapeHtml(item.email)}</small>
                    </div>
                    <button type="button"
                            class="${item.canSendRequest ? 'btn btn-primary' : 'btn btn-outline'} social-result-action"
                            data-send-friend-request
                            data-target-user-id="${item.userId}"
                            ${item.canSendRequest ? '' : 'disabled'}>
                        <i class="fa-solid ${item.canSendRequest ? 'fa-user-plus' : 'fa-check'}"></i>
                        <span>${escapeHtml(item.actionLabel)}</span>
                    </button>
                </div>`;
        }).join('');
    };

    const setCandidateState = (userId, state) => {
        const button = friendModal?.querySelector(`[data-candidate-user-id="${userId}"] [data-send-friend-request]`);
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        if (state === 'friend') {
            button.disabled = true;
            button.className = 'btn btn-outline social-result-action';
            button.innerHTML = '<i class="fa-solid fa-check"></i><span>Ban be</span>';
            return;
        }

        if (state === 'pending-sent') {
            button.disabled = true;
            button.className = 'btn btn-outline social-result-action';
            button.innerHTML = '<i class="fa-solid fa-paper-plane"></i><span>Da gui</span>';
            return;
        }

        if (state === 'pending-received') {
            button.disabled = true;
            button.className = 'btn btn-outline social-result-action';
            button.innerHTML = '<i class="fa-solid fa-bell"></i><span>Mo thong bao</span>';
            return;
        }

        button.disabled = false;
        button.className = 'btn btn-primary social-result-action';
        button.innerHTML = '<i class="fa-solid fa-user-plus"></i><span>Ket ban</span>';
    };

    const performCandidateSearch = async (term) => {
        if (!friendModal || connection.state !== signalRClient.HubConnectionState.Connected) {
            return;
        }

        const currentToken = ++searchToken;
        setFriendSearchStatus('Dang tim thanh vien...');

        try {
            const items = await connection.invoke('SearchFriendCandidates', term);
            if (currentToken !== searchToken) {
                return;
            }

            renderCandidateResults(items);
            setFriendSearchStatus(items.length > 0 ? 'Chon mot thanh vien de gui loi moi ket ban.' : 'Khong tim thay ket qua phu hop.');
        } catch {
            if (currentToken !== searchToken) {
                return;
            }

            setFriendSearchStatus('Khong the tim kiem thanh vien luc nay.');
        }
    };

    const scheduleCandidateSearch = (term) => {
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(() => performCandidateSearch(term), 180);
    };

    const openFriendModal = async () => {
        if (!(friendModal instanceof HTMLElement)) {
            return;
        }

        friendModal.hidden = false;
        friendModal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('profile-modal-open');
        friendModal.querySelector('[data-friend-search-input]')?.focus();
        await performCandidateSearch('');
    };

    const closeFriendModal = () => {
        if (!(friendModal instanceof HTMLElement)) {
            return;
        }

        friendModal.hidden = true;
        friendModal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('profile-modal-open');
    };

    const ensureNotificationList = () => {
        if (notificationList instanceof HTMLElement) {
            return notificationList;
        }

        if (!(notificationPage instanceof HTMLElement)) {
            return null;
        }

        notificationPage.querySelector('.empty-state')?.remove();
        const surface = notificationPage.querySelector('.surface');
        if (!(surface instanceof HTMLElement)) {
            return null;
        }

        notificationList = document.createElement('div');
        notificationList.className = 'notice-list';
        notificationList.setAttribute('data-notification-list', '');
        surface.appendChild(notificationList);
        return notificationList;
    };

    const renderNotificationItem = (notification) => {
        const actorDisplayName = notification.actorDisplayName || 'Zenpace';
        const avatarMarkup = notification.actorAvatarUrl
            ? `<img src="${escapeHtml(notification.actorAvatarUrl)}" alt="${escapeHtml(actorDisplayName)}" />`
            : `<span>${escapeHtml(getInitials(actorDisplayName, 'Z'))}</span>`;
        const createdAt = notification.createdAt
            ? new Date(notification.createdAt).toLocaleString('vi-VN', { month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit' })
            : '';

        return `
            <div class="glass-card notice-item ${notification.isRead ? '' : 'notice-item--unread'}"
                 data-notification-item
                 data-notification-id="${notification.id}"
                 data-friend-request-id="${notification.friendRequestId ?? ''}">
                <div class="notice-content">
                    <div class="notice-head">
                        <div class="notice-avatar">${avatarMarkup}</div>
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
                    ${!notification.isRead ? `
                        <form action="/Notification/MarkAsRead" method="post" data-mark-notification-read>
                            <input type="hidden" name="id" value="${notification.id}" />
                            <button type="submit" class="notice-action" title="Danh dau da doc">
                                <i class="fa-solid fa-check"></i>
                            </button>
                        </form>` : ''}
                </div>
            </div>`;
    };

    const prependNotification = (notification) => {
        const list = ensureNotificationList();
        if (!(list instanceof HTMLElement)) {
            return;
        }

        const existing = list.querySelector(`[data-notification-id="${notification.id}"]`);
        if (existing instanceof HTMLElement) {
            existing.outerHTML = renderNotificationItem(notification);
            return;
        }

        list.insertAdjacentHTML('afterbegin', renderNotificationItem(notification));
    };

    const updateNotificationResolution = (requestId, status) => {
        const item = notificationList?.querySelector(`[data-friend-request-id="${requestId}"]`);
        if (!(item instanceof HTMLElement)) {
            return;
        }

        item.classList.remove('notice-item--unread');
        item.querySelectorAll('[data-notification-accept], [data-notification-decline]').forEach((element) => element.remove());
        item.querySelectorAll('[data-mark-notification-read]').forEach((element) => element.remove());

        const state = item.querySelector('[data-notification-state]');
        if (state instanceof HTMLElement) {
            state.textContent = 'Da doc';
        }

        const friendRequestState = item.querySelector('[data-friend-request-state]');
        if (friendRequestState instanceof HTMLElement) {
            friendRequestState.textContent = status === 'Accepted' ? 'Da chap nhan' : 'Da tu choi';
        }
    };

    const renderProfileActions = () => {
        if (!(profileSocial instanceof HTMLElement)) {
            return;
        }

        const isFriend = profileSocial.dataset.isFriend === 'true';
        const hasOutgoing = profileSocial.dataset.hasOutgoingRequest === 'true';
        const hasIncoming = profileSocial.dataset.hasIncomingRequest === 'true';
        const isBlockedByViewer = profileSocial.dataset.messageBlockedByViewer === 'true';

        const primaryAction = isFriend
            ? '<button type="button" class="btn btn-outline" data-social-remove-friend><i class="fa-solid fa-user-minus"></i><span>Xoa ban</span></button>'
            : hasOutgoing
                ? '<button type="button" class="btn btn-outline" disabled><i class="fa-solid fa-paper-plane"></i><span>Da gui loi moi</span></button>'
                : hasIncoming
                    ? '<a href="/Notification" class="btn btn-primary"><i class="fa-solid fa-bell"></i><span>Mo thong bao de chap nhan</span></a>'
                    : '<button type="button" class="btn btn-primary" data-social-send-request><i class="fa-solid fa-user-plus"></i><span>Ket ban</span></button>';

        profileSocial.innerHTML = `
            ${primaryAction}
            <button type="button" class="btn btn-text" data-social-toggle-block>
                <i class="fa-solid fa-ban"></i>
                <span>${isBlockedByViewer ? 'Bo chan tin nhan' : 'Chan tin nhan'}</span>
            </button>`;
    };

    const renderProfileStatus = () => {
        if (!(profileSocial instanceof HTMLElement) || !(profileSocialStatus instanceof HTMLElement)) {
            return;
        }

        const isFriend = profileSocial.dataset.isFriend === 'true';
        const hasOutgoing = profileSocial.dataset.hasOutgoingRequest === 'true';
        const hasIncoming = profileSocial.dataset.hasIncomingRequest === 'true';
        const isBlockedByViewer = profileSocial.dataset.messageBlockedByViewer === 'true';
        const isBlockedByOtherUser = profileSocial.dataset.messageBlockedByOtherUser === 'true';

        profileSocialStatus.textContent = isFriend
            ? 'Hai ban dang la ban be tren Zenpace.'
            : hasOutgoing
                ? 'Ban da gui loi moi ket ban va dang cho phan hoi.'
                : hasIncoming
                    ? 'Nguoi dung nay da gui loi moi ket ban cho ban. Hay vao thong bao de chap nhan.'
                    : isBlockedByViewer
                        ? 'Ban dang chan tin nhan tu nguoi dung nay.'
                        : isBlockedByOtherUser
                            ? 'Nguoi dung nay dang chan tin nhan voi ban.'
                            : 'Ban co the ket ban, chat rieng hoac quan ly quyen nhan tin ngay tai day.';
    };

    const syncProfileState = (state) => {
        if (!(profileSocial instanceof HTMLElement)) {
            return;
        }

        Object.entries(state).forEach(([key, value]) => {
            if (typeof value === 'boolean') {
                profileSocial.dataset[key] = `${value}`;
            }
        });

        renderProfileActions();
        renderProfileStatus();
    };

    const updateChatState = (payload) => {
        const chatPanel = document.querySelector('[data-chat-panel]');
        if (!(chatPanel instanceof HTMLElement)) {
            return;
        }

        const targetUserId = Number.parseInt(chatPanel.dataset.targetUserId || '', 10);
        if (targetUserId !== payload.targetUserId) {
            return;
        }

        let message = '';
        if (payload.isMessageBlockedByViewer) {
            message = 'Ban da chan tin nhan voi nguoi dung nay.';
        } else if (payload.isMessageBlockedByOtherUser) {
            message = 'Nguoi dung nay da chan tin nhan voi ban.';
        }

        chatPanel.dataset.chatCanSend = payload.isConversationBlocked ? 'false' : 'true';
        chatPanel.dataset.chatBlockMessage = message;

        const banner = document.querySelector('[data-chat-banner]');
        if (banner instanceof HTMLElement) {
            banner.textContent = message;
            banner.hidden = !message;
        } else if (message) {
            const nextBanner = document.createElement('div');
            nextBanner.className = 'profile-chat-banner is-error';
            nextBanner.setAttribute('data-chat-banner', '');
            nextBanner.textContent = message;
            chatPanel.insertBefore(nextBanner, chatPanel.querySelector('[data-chat-form]'));
        }

        document.dispatchEvent(new CustomEvent('zenpace:chat-state-changed'));
    };

    const bindHomeSocial = () => {
        document.querySelector('[data-open-friend-modal]')?.addEventListener('click', () => openFriendModal());
        document.querySelectorAll('[data-close-friend-modal]').forEach((element) => element.addEventListener('click', () => closeFriendModal()));

        friendModal?.querySelector('[data-friend-search-input]')?.addEventListener('input', (event) => {
            if (event.currentTarget instanceof HTMLInputElement) {
                scheduleCandidateSearch(event.currentTarget.value);
            }
        });

        friendModal?.querySelector('[data-friend-search-button]')?.addEventListener('click', () => {
            const input = friendModal.querySelector('[data-friend-search-input]');
            if (input instanceof HTMLInputElement) {
                performCandidateSearch(input.value);
            }
        });

        friendModal?.querySelector('[data-friend-search-results]')?.addEventListener('click', async (event) => {
            const button = event.target instanceof Element ? event.target.closest('[data-send-friend-request]') : null;
            if (!(button instanceof HTMLButtonElement)) {
                return;
            }

            const targetUserId = Number.parseInt(button.dataset.targetUserId || '', 10);
            if (!Number.isInteger(targetUserId) || targetUserId <= 0) {
                return;
            }

            button.disabled = true;
            try {
                await connection.invoke('SendFriendRequest', targetUserId);
                setCandidateState(targetUserId, 'pending-sent');
                if (getProfileTargetUserId() === targetUserId) {
                    syncProfileState({ isFriend: false, hasOutgoingRequest: true, hasIncomingRequest: false });
                }
            } catch {
                button.disabled = false;
                setFriendSearchStatus('Khong the gui loi moi ket ban luc nay.');
            }
        });

        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && friendModal instanceof HTMLElement && !friendModal.hidden) {
                closeFriendModal();
            }
        });
    };

    const bindNotificationPage = () => {
        notificationPage?.addEventListener('click', async (event) => {
            const acceptButton = event.target instanceof Element ? event.target.closest('[data-notification-accept]') : null;
            if (acceptButton instanceof HTMLButtonElement) {
                const requestId = Number.parseInt(acceptButton.dataset.friendRequestId || '', 10);
                if (Number.isInteger(requestId) && requestId > 0) {
                    acceptButton.disabled = true;
                    try {
                        await connection.invoke('AcceptFriendRequest', requestId);
                    } catch {
                        acceptButton.disabled = false;
                    }
                }
                return;
            }

            const declineButton = event.target instanceof Element ? event.target.closest('[data-notification-decline]') : null;
            if (!(declineButton instanceof HTMLButtonElement)) {
                return;
            }

            const requestId = Number.parseInt(declineButton.dataset.friendRequestId || '', 10);
            if (Number.isInteger(requestId) && requestId > 0) {
                declineButton.disabled = true;
                try {
                    await connection.invoke('DeclineFriendRequest', requestId);
                } catch {
                    declineButton.disabled = false;
                }
            }
        });
    };

    const bindProfileSocial = () => {
        if (!(profileSocial instanceof HTMLElement)) {
            return;
        }

        renderProfileActions();
        renderProfileStatus();

        profileSocial.addEventListener('click', async (event) => {
            const action = event.target instanceof Element ? event.target.closest('[data-social-send-request], [data-social-remove-friend], [data-social-toggle-block]') : null;
            if (!(action instanceof HTMLElement)) {
                return;
            }

            const targetUserId = getProfileTargetUserId();
            if (!Number.isInteger(targetUserId) || targetUserId <= 0) {
                return;
            }

            try {
                if (action.hasAttribute('data-social-send-request')) {
                    await connection.invoke('SendFriendRequest', targetUserId);
                    syncProfileState({ isFriend: false, hasOutgoingRequest: true, hasIncomingRequest: false });
                    return;
                }

                if (action.hasAttribute('data-social-remove-friend')) {
                    await connection.invoke('RemoveFriend', targetUserId);
                    syncProfileState({ isFriend: false, hasOutgoingRequest: false, hasIncomingRequest: false });
                    return;
                }

                await connection.invoke('ToggleMessageBlock', targetUserId);
            } catch {
                renderProfileStatus();
            }
        });
    };

    connection.on('NotificationCountChanged', (payload) => setUnreadCount(Number.parseInt(`${payload.unreadCount ?? 0}`, 10)));
    connection.on('NotificationUpserted', (notification) => prependNotification(notification));
    connection.on('FriendRequestResolved', (payload) => {
        updateNotificationResolution(payload.requestId, payload.status);
        setUnreadCount(Number.parseInt(`${payload.unreadCount ?? 0}`, 10));
    });
    connection.on('FriendRequestStateChanged', (payload) => {
        const targetUserId = Number.parseInt(`${payload.userId ?? ''}`, 10);
        const state = `${payload.state ?? ''}`;
        if (!Number.isInteger(targetUserId) || targetUserId <= 0) {
            return;
        }

        setCandidateState(targetUserId, state);
        if (getProfileTargetUserId() === targetUserId) {
            syncProfileState({
                isFriend: state === 'friend',
                hasOutgoingRequest: state === 'pending-sent',
                hasIncomingRequest: state === 'pending-received'
            });
        }
    });
    connection.on('FriendshipAdded', (friend) => {
        upsertFriendCard(friend);
        setCandidateState(friend.userId, 'friend');
        if (getProfileTargetUserId() === friend.userId) {
            syncProfileState({
                isFriend: true,
                hasOutgoingRequest: false,
                hasIncomingRequest: false,
                isMessageBlockedByViewer: !!friend.isMessageBlockedByViewer,
                isMessageBlockedByOtherUser: !!friend.isMessageBlockedByOtherUser
            });
        }
    });
    connection.on('FriendshipRemoved', (payload) => {
        const friendUserId = Number.parseInt(`${payload.friendUserId ?? ''}`, 10);
        if (!Number.isInteger(friendUserId) || friendUserId <= 0) {
            return;
        }

        removeFriendCard(friendUserId);
        setCandidateState(friendUserId, 'none');
        if (getProfileTargetUserId() === friendUserId) {
            syncProfileState({ isFriend: false, hasOutgoingRequest: false, hasIncomingRequest: false });
        }
    });
    connection.on('MessageBlockChanged', (payload) => {
        const targetUserId = Number.parseInt(`${payload.targetUserId ?? ''}`, 10);
        if (!Number.isInteger(targetUserId) || targetUserId <= 0) {
            return;
        }

        updateFriendCardBlockState(targetUserId, payload);
        updateChatState(payload);
        if (getProfileTargetUserId() === targetUserId) {
            syncProfileState({
                isMessageBlockedByViewer: !!payload.isMessageBlockedByViewer,
                isMessageBlockedByOtherUser: !!payload.isMessageBlockedByOtherUser
            });
        }
    });

    bindHomeSocial();
    bindNotificationPage();
    bindProfileSocial();

    connection.start().catch(() => setFriendSearchStatus('Realtime tam thoi khong kha dung.'));
})();
