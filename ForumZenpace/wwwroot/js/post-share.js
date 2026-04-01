import { postSocialAction } from './social/utils.js';

const shareModal = document.querySelector('[data-share-modal]');

if (shareModal instanceof HTMLElement) {
    const shareDialog = shareModal.querySelector('.post-share-modal__dialog');
    const shareTitle = shareModal.querySelector('[data-share-post-title]');
    const shareOpenLink = shareModal.querySelector('[data-share-open-link]');
    const shareLinkOutput = shareModal.querySelector('[data-share-link-output]');
    const shareCopyButton = shareModal.querySelector('[data-share-copy-link]');
    const shareSearchInput = shareModal.querySelector('[data-share-friend-search]');
    const shareFriendList = shareModal.querySelector('[data-share-friend-list]');
    const shareFriendEmpty = shareModal.querySelector('[data-share-friend-empty]');
    const shareChatStatus = shareModal.querySelector('[data-share-chat-status]');
    const shareLinkStatus = shareModal.querySelector('[data-share-link-status]');
    const shareSendButtons = Array.from(shareModal.querySelectorAll('[data-share-send]'));
    const shareTriggerButtons = Array.from(document.querySelectorAll('[data-open-share-modal]'));
    let shareLastFocusedElement = null;

    const normalizeSearchText = (value) => `${value || ''}`
        .trim()
        .toLowerCase()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '');

    const setStatusMessage = (element, message, state = '') => {
        if (!(element instanceof HTMLElement)) {
            return;
        }

        element.textContent = message;
        element.classList.toggle('is-success', state === 'success');
        element.classList.toggle('is-error', state === 'error');
    };

    const resetStatusMessage = (element) => {
        if (!(element instanceof HTMLElement)) {
            return;
        }

        setStatusMessage(element, element.dataset.defaultMessage || '', '');
    };

    const getShareFocusableElements = () => {
        if (!(shareDialog instanceof HTMLElement)) {
            return [];
        }

        return Array.from(shareDialog.querySelectorAll(
            'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
        )).filter((element) => element instanceof HTMLElement && !element.hasAttribute('hidden'));
    };

    const getCurrentSharePost = () => ({
        postId: Number.parseInt(shareModal.dataset.postId || '', 10),
        postTitle: shareModal.dataset.postTitle || '',
        postUrl: shareModal.dataset.postUrl || ''
    });

    const updateFriendFilter = () => {
        if (!(shareFriendList instanceof HTMLElement)) {
            return;
        }

        const keyword = shareSearchInput instanceof HTMLInputElement
            ? normalizeSearchText(shareSearchInput.value)
            : '';
        let visibleCount = 0;

        shareFriendList.querySelectorAll('[data-share-friend-item]').forEach((item) => {
            if (!(item instanceof HTMLElement)) {
                return;
            }

            const searchValue = normalizeSearchText(item.dataset.shareSearchValue || '');
            const isVisible = keyword.length === 0 || searchValue.includes(keyword);
            item.hidden = !isVisible;
            if (isVisible) {
                visibleCount += 1;
            }
        });

        if (shareFriendEmpty instanceof HTMLElement) {
            shareFriendEmpty.hidden = visibleCount > 0;
        }

        if (shareChatStatus instanceof HTMLElement) {
            if (visibleCount === 0) {
                setStatusMessage(shareChatStatus, 'Không tìm thấy bạn bè phù hợp.', 'error');
            } else {
                resetStatusMessage(shareChatStatus);
            }
        }
    };

    const resetShareButtons = () => {
        shareSendButtons.forEach((button) => {
            if (!(button instanceof HTMLButtonElement)) {
                return;
            }

            if (button.dataset.defaultHtml) {
                button.innerHTML = button.dataset.defaultHtml;
            }

            button.disabled = button.dataset.initiallyDisabled === 'true';
        });
    };

    const openShareModal = (button) => {
        if (!(button instanceof HTMLElement)) {
            return;
        }

        const postId = Number.parseInt(button.dataset.sharePostId || '', 10);
        const postTitle = button.dataset.sharePostTitle || 'Bài viết';
        const postUrl = button.dataset.sharePostUrl || '';
        if (!Number.isInteger(postId) || !postUrl) {
            return;
        }

        shareLastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        shareModal.dataset.postId = `${postId}`;
        shareModal.dataset.postTitle = postTitle;
        shareModal.dataset.postUrl = postUrl;

        if (shareTitle instanceof HTMLElement) {
            shareTitle.textContent = postTitle;
        }

        if (shareOpenLink instanceof HTMLAnchorElement) {
            shareOpenLink.href = postUrl;
        }

        if (shareLinkOutput instanceof HTMLInputElement) {
            shareLinkOutput.value = postUrl;
        }

        resetStatusMessage(shareChatStatus);
        resetStatusMessage(shareLinkStatus);
        resetShareButtons();

        if (shareSearchInput instanceof HTMLInputElement) {
            shareSearchInput.value = '';
        }

        updateFriendFilter();

        shareModal.hidden = false;
        shareModal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('profile-modal-open');

        window.requestAnimationFrame(() => {
            if (shareSearchInput instanceof HTMLInputElement) {
                shareSearchInput.focus();
                return;
            }

            if (shareCopyButton instanceof HTMLButtonElement) {
                shareCopyButton.focus();
            }
        });
    };

    const closeShareModal = () => {
        shareModal.hidden = true;
        shareModal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('profile-modal-open');

        if (shareLastFocusedElement instanceof HTMLElement) {
            shareLastFocusedElement.focus();
        }

        shareLastFocusedElement = null;
    };

    const copyShareLink = async () => {
        const { postUrl } = getCurrentSharePost();
        if (!postUrl) {
            setStatusMessage(shareLinkStatus, 'Không thể tạo liên kết cho bài viết này.', 'error');
            return;
        }

        try {
            if (navigator.clipboard?.writeText) {
                await navigator.clipboard.writeText(postUrl);
            } else if (shareLinkOutput instanceof HTMLInputElement) {
                shareLinkOutput.focus();
                shareLinkOutput.select();
                shareLinkOutput.setSelectionRange(0, shareLinkOutput.value.length);
                const copied = typeof document.execCommand === 'function' && document.execCommand('copy');
                if (!copied) {
                    throw new Error('copy-failed');
                }
            } else {
                throw new Error('copy-failed');
            }

            setStatusMessage(shareLinkStatus, 'Đã sao chép liên kết bài viết.', 'success');
        } catch {
            setStatusMessage(shareLinkStatus, 'Không thể sao chép liên kết lúc này.', 'error');
        }
    };

    shareTriggerButtons.forEach((button) => {
        if (!(button instanceof HTMLElement)) {
            return;
        }

        button.addEventListener('click', () => openShareModal(button));
    });

    shareModal.querySelectorAll('[data-close-share-modal]').forEach((button) => {
        button.addEventListener('click', closeShareModal);
    });

    if (shareSearchInput instanceof HTMLInputElement) {
        shareSearchInput.addEventListener('input', updateFriendFilter);
    }

    if (shareCopyButton instanceof HTMLButtonElement) {
        shareCopyButton.addEventListener('click', copyShareLink);
    }

    if (shareFriendList instanceof HTMLElement) {
        shareFriendList.addEventListener('click', async (event) => {
            const button = event.target instanceof Element ? event.target.closest('[data-share-send]') : null;
            if (!(button instanceof HTMLButtonElement)) {
                return;
            }

            const { postId } = getCurrentSharePost();
            const targetUserId = Number.parseInt(button.dataset.targetUserId || '', 10);
            const username = button.dataset.username || '';
            if (!Number.isInteger(postId) || !Number.isInteger(targetUserId) || !username) {
                setStatusMessage(shareChatStatus, 'Thiếu thông tin để gửi bài viết.', 'error');
                return;
            }

            const originalHtml = button.dataset.defaultHtml || button.innerHTML;
            button.disabled = true;
            button.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i><span>Đang gửi</span>';
            setStatusMessage(shareChatStatus, 'Đang gửi bài viết vào cuộc trò chuyện...', '');

            try {
                const response = await postSocialAction('/Post/ShareToFriend', {
                    postId,
                    targetUserId,
                    username
                });

                setStatusMessage(shareChatStatus, response.message || 'Đã gửi bài viết.', 'success');
                button.innerHTML = '<i class="fa-solid fa-check"></i><span>Đã gửi</span>';

                window.setTimeout(() => {
                    button.innerHTML = originalHtml;
                    button.disabled = button.dataset.initiallyDisabled === 'true';
                }, 1600);
            } catch (error) {
                const message = error instanceof Error && error.message
                    ? error.message
                    : 'Không thể gửi bài viết lúc này.';
                setStatusMessage(shareChatStatus, message, 'error');
                button.innerHTML = originalHtml;
                button.disabled = button.dataset.initiallyDisabled === 'true';
            }
        });
    }

    document.addEventListener('keydown', (event) => {
        if (shareModal.hidden) {
            return;
        }

        if (event.key === 'Escape') {
            closeShareModal();
            return;
        }

        if (event.key !== 'Tab') {
            return;
        }

        const focusableElements = getShareFocusableElements();
        if (focusableElements.length === 0) {
            return;
        }

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

    if (shareChatStatus instanceof HTMLElement) {
        shareChatStatus.dataset.defaultMessage = shareChatStatus.textContent?.trim() || '';
    }

    if (shareLinkStatus instanceof HTMLElement) {
        shareLinkStatus.dataset.defaultMessage = shareLinkStatus.textContent?.trim() || '';
    }

    shareSendButtons.forEach((button) => {
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        button.dataset.defaultHtml = button.innerHTML;
        button.dataset.initiallyDisabled = button.disabled ? 'true' : 'false';
    });
}
