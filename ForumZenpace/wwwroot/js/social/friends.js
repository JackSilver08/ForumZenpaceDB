import { 
    friendModal, friendModalDialog, friendRail, friendList, friendPrevButton, friendNextButton,
    connection, isRealtimeAvailable, getProfileTargetUserId, getCurrentReturnUrl,
    profileSocial
} from './state.js';
import { escapeHtml, getInitials, fetchJson, postSocialAction, getSocialData } from './utils.js';
import { syncProfileState, renderProfileStatus } from './profile.js';

let friendRailResizeObserver = null;
let friendRailFrame = 0;
let searchTimer = 0;
let searchToken = 0;
let friendModalLastFocusedElement = null;

export const getFriendModalFocusableElements = () => {
    if (!(friendModalDialog instanceof HTMLElement)) return [];
    return Array.from(friendModalDialog.querySelectorAll(
        'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )).filter((element) => element instanceof HTMLElement && !element.hasAttribute('hidden'));
};

const setFriendSearchStatus = (message) => {
    const status = friendModal?.querySelector('[data-friend-search-status]');
    if (status instanceof HTMLElement) status.textContent = message;
};

const setFriendNavVisibility = (button, visible) => {
    if (!(button instanceof HTMLButtonElement)) return;
    button.hidden = !visible;
    button.disabled = !visible;
};

const getFriendRailGap = () => {
    if (!(friendList instanceof HTMLElement)) return 0;
    const styles = window.getComputedStyle(friendList);
    const gapValue = Number.parseFloat(styles.columnGap || styles.gap || '0');
    return Number.isFinite(gapValue) ? gapValue : 0;
};

const getFriendRailStep = () => {
    if (!(friendList instanceof HTMLElement)) return 0;
    const firstItem = friendList.querySelector('[data-friend-card], [data-friend-empty]');
    if (!(firstItem instanceof HTMLElement)) return Math.max(friendList.clientWidth * 0.82, 0);
    return firstItem.getBoundingClientRect().width + getFriendRailGap();
};

export const updateFriendRailControls = () => {
    if (!(friendRail instanceof HTMLElement) || !(friendList instanceof HTMLElement)) return;

    const maxScrollLeft = Math.max(0, friendList.scrollWidth - friendList.clientWidth);
    const hasOverflow = maxScrollLeft > 4;

    if (!hasOverflow && friendList.scrollLeft > 0) friendList.scrollLeft = 0;

    const canScrollPrev = hasOverflow && friendList.scrollLeft > 4;
    const canScrollNext = hasOverflow && friendList.scrollLeft < maxScrollLeft - 4;

    friendRail.classList.toggle('is-scrollable', hasOverflow);
    friendRail.classList.toggle('can-scroll-prev', canScrollPrev);
    friendRail.classList.toggle('can-scroll-next', canScrollNext);

    setFriendNavVisibility(friendPrevButton, canScrollPrev);
    setFriendNavVisibility(friendNextButton, canScrollNext);
};

export const scheduleFriendRailUpdate = () => {
    if (friendRailFrame) return;
    friendRailFrame = window.requestAnimationFrame(() => {
        friendRailFrame = 0;
        updateFriendRailControls();
    });
};

export const scrollFriendRail = (direction) => {
    if (!(friendList instanceof HTMLElement)) return;
    const step = getFriendRailStep();
    if (step <= 0) return;
    friendList.scrollBy({
        left: direction * step,
        behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth'
    });
};

export const ensureFriendEmptyState = () => {
    if (!(friendList instanceof HTMLElement) || friendList.querySelector('[data-friend-card]')) return;
    friendList.innerHTML = `
        <div class="glass-panel social-card social-card--empty" data-friend-empty>
            <span class="social-card-icon"><i class="fa-regular fa-user"></i></span>
            <span class="social-card-copy">
                <strong>Chua co ban</strong>
                <span>Bat dau ket noi tu o Them ban</span>
            </span>
        </div>`;
    scheduleFriendRailUpdate();
};

export const removeFriendEmptyState = () => friendList?.querySelector('[data-friend-empty]')?.remove();

export const getFriendCardState = (friend) => {
    if (friend.hasActiveStory && friend.latestStoryId) {
        return { state: 'story', label: `Xem khoanh khac cua @${friend.username}`, icon: 'fa-circle-play', hidden: true };
    }
    if (friend.isMessageBlockedByViewer) {
        return { state: 'blocked-by-viewer', label: 'Ban dang chan tin nhan voi nguoi nay.', icon: 'fa-ban', hidden: false };
    }
    if (friend.isMessageBlockedByOtherUser) {
        return { state: 'blocked-by-other', label: 'Nguoi nay dang chan tin nhan voi ban.', icon: 'fa-shield-halved', hidden: false };
    }
    return { state: 'connected', label: `Trang ca nhan cua @${friend.username}`, icon: 'fa-user-group', hidden: true };
};

export const renderFriendCard = (friend) => {
    const friendState = getFriendCardState(friend);
    const hasActiveStory = !!friend.hasActiveStory && Number.isInteger(Number.parseInt(`${friend.latestStoryId ?? ''}`, 10));
    const targetUrl = hasActiveStory
        ? `/Story/Viewer/${encodeURIComponent(friend.latestStoryId)}`
        : `/Profile/user/${encodeURIComponent(friend.username)}`;
    const avatarMarkup = friend.avatarUrl
        ? `<img src="${escapeHtml(friend.avatarUrl)}" alt="${escapeHtml(friend.displayName)}" />`
        : `<span>${escapeHtml(getInitials(friend.displayName, friend.username))}</span>`;
    const storyPill = hasActiveStory
        ? `<span class="social-story-pill">${escapeHtml(`${friend.activeStoryCount || 1} story`)}</span>`
        : '';
    const storyMeta = hasActiveStory
        ? `<span>${friend.hasUnviewedStory ? 'Chua xem' : 'Da xem'}</span>`
        : '';

    return `
        <a href="${targetUrl}"
           class="glass-panel social-card social-card--friend${hasActiveStory ? ' social-card--story' : ''}${friend.hasUnviewedStory ? ' social-card--story-fresh' : ''}"
           data-friend-card
           data-friend-user-id="${friend.userId}"
           data-friend-username="${escapeHtml(friend.username)}"
           data-friend-state="${friendState.state}"
           title="${escapeHtml(friendState.label)}">
            <span class="social-friend-badge${friendState.hidden ? ' is-hidden' : ''}" data-friend-status-badge aria-hidden="true">
                <i class="fa-solid ${friendState.icon}"></i>
            </span>
            ${storyPill}
            <span class="social-avatar social-avatar--story${hasActiveStory ? ' has-story' : ''} avatar-link">
                ${avatarMarkup}
                <span class="presence-dot" data-presence-user-id="${friend.userId}"></span>
            </span>
            <span class="social-card-copy">
                <strong>@${escapeHtml(friend.username)}</strong>
                ${storyMeta}
            </span>
        </a>`;
};

export const upsertFriendCard = (friend) => {
    if (!(friendList instanceof HTMLElement)) return;
    removeFriendEmptyState();
    const existingCard = friendList.querySelector(`[data-friend-user-id="${friend.userId}"]`);
    if (existingCard instanceof HTMLElement) {
        existingCard.outerHTML = renderFriendCard(friend);
        scheduleFriendRailUpdate();
        return;
    }
    friendList.insertAdjacentHTML('afterbegin', renderFriendCard(friend));
    scheduleFriendRailUpdate();
};

export const removeFriendCard = (friendUserId) => {
    friendList?.querySelector(`[data-friend-user-id="${friendUserId}"]`)?.remove();
    ensureFriendEmptyState();
    scheduleFriendRailUpdate();
};

export const updateFriendCardBlockState = (targetUserId, payload) => {
    const card = friendList?.querySelector(`[data-friend-user-id="${targetUserId}"]`);
    if (!(card instanceof HTMLElement)) return;

    const badge = card.querySelector('[data-friend-status-badge]');
    const icon = badge?.querySelector('i');

    let state = 'connected';
    let label = `Trang ca nhan cua @${card.dataset.friendUsername || ''}`;
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
    if (!(container instanceof HTMLElement)) return;

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
                <div class="social-result-avatar avatar-link">
                    ${avatarMarkup}
                    <span class="presence-dot" data-presence-user-id="${item.userId}"></span>
                </div>
                <div class="social-result-copy">
                    <strong>${escapeHtml(item.displayName)}</strong>
                    <span>@${escapeHtml(item.username)}</span>
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

export const setCandidateState = (userId, state) => {
    const button = friendModal?.querySelector(`[data-candidate-user-id="${userId}"] [data-send-friend-request]`);
    if (!(button instanceof HTMLButtonElement)) return;

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

export const performCandidateSearch = async (term) => {
    if (!friendModal) return;

    const currentToken = ++searchToken;
    setFriendSearchStatus('Dang tim thanh vien...');

    try {
        const items = isRealtimeAvailable()
            ? await connection.invoke('SearchFriendCandidates', term)
            : await getSocialData('/Social/SearchFriendCandidates', { term });
        if (currentToken !== searchToken) return;

        renderCandidateResults(items);
        
        let statusText = 'Khong tim thay ket qua phu hop.';
        if (items.length > 0) {
            statusText = term.trim() === '' ? 'Goi y ket ban cho rieng ban:' : 'Chon mot thanh vien de gui loi moi ket ban.';
        }
        setFriendSearchStatus(statusText);
    } catch {
        if (currentToken !== searchToken) return;
        setFriendSearchStatus('Khong the tim kiem thanh vien luc nay.');
    }
};

export const scheduleCandidateSearch = (term) => {
    window.clearTimeout(searchTimer);
    searchTimer = window.setTimeout(() => performCandidateSearch(term), 180);
};

export const openFriendModal = async () => {
    if (!(friendModal instanceof HTMLElement)) return;
    friendModalLastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    friendModal.hidden = false;
    friendModal.setAttribute('aria-hidden', 'false');
    document.body.classList.add('profile-modal-open');
    friendModal.querySelector('[data-friend-search-input]')?.focus();
    await performCandidateSearch('');
};

export const closeFriendModal = () => {
    if (!(friendModal instanceof HTMLElement)) return;
    friendModal.hidden = true;
    friendModal.setAttribute('aria-hidden', 'true');
    document.body.classList.remove('profile-modal-open');

    if (friendModalLastFocusedElement instanceof HTMLElement) {
        friendModalLastFocusedElement.focus();
    }
    friendModalLastFocusedElement = null;
};

export const bindHomeSocial = () => {
    if (friendPrevButton instanceof HTMLButtonElement) {
        friendPrevButton.addEventListener('click', () => scrollFriendRail(-1));
    }
    if (friendNextButton instanceof HTMLButtonElement) {
        friendNextButton.addEventListener('click', () => scrollFriendRail(1));
    }

    friendList?.addEventListener('scroll', () => scheduleFriendRailUpdate(), { passive: true });
    window.addEventListener('resize', () => scheduleFriendRailUpdate());

    if ('ResizeObserver' in window && friendList instanceof HTMLElement) {
        friendRailResizeObserver = new ResizeObserver(() => scheduleFriendRailUpdate());
        friendRailResizeObserver.observe(friendList);
    }

    document.querySelector('[data-open-friend-modal]')?.addEventListener('click', openFriendModal);
    document.querySelectorAll('[data-close-friend-modal]').forEach((elem) => elem.addEventListener('click', closeFriendModal));

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
        if (!(button instanceof HTMLButtonElement)) return;

        const targetUserId = Number.parseInt(button.dataset.targetUserId || '', 10);
        if (!Number.isInteger(targetUserId) || targetUserId <= 0) return;

        button.disabled = true;
        try {
            if (isRealtimeAvailable()) {
                await connection.invoke('SendFriendRequest', targetUserId);
            } else {
                await postSocialAction('/Social/SendFriendRequest', { targetUserId, returnUrl: getCurrentReturnUrl() });
            }

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
            return;
        }

        if (event.key !== 'Tab' || !(friendModal instanceof HTMLElement) || friendModal.hidden) return;

        const focusableElements = getFriendModalFocusableElements();
        if (focusableElements.length === 0) return;

        const firstElement = focusableElements[0];
        const lastElement = focusableElements[focusableElements.length - 1];
        const activeElement = document.activeElement;

        if (event.shiftKey && activeElement === firstElement) {
            event.preventDefault();
            lastElement.focus();
            return;
        }

        if (!event.shiftKey && activeElement === lastElement) {
            event.preventDefault();
            firstElement.focus();
        }
    });
    
    scheduleFriendRailUpdate();
};
