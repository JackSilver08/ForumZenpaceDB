import { connection, isRealtimeAvailable } from './social/state.js';

(() => {
    const detailRoot = document.querySelector('[data-post-detail]');
    if (!(detailRoot instanceof HTMLElement)) {
        return;
    }

    const postId = Number.parseInt(detailRoot.dataset.postId || '', 10);
    if (!Number.isInteger(postId) || postId <= 0) {
        return;
    }

    const likeButton = detailRoot.querySelector('[data-post-like-button]');
    const commentCount = detailRoot.querySelector('[data-post-comment-count]');
    const commentList = detailRoot.querySelector('[data-post-comment-list]');
    const reportBox = document.getElementById('reportBox');
    let joinedRealtime = false;
    let refreshPromise = null;
    let refreshQueued = false;

    const setSubmitButtonBusy = (form, isBusy) => {
        const submitButton = form.querySelector('button[type="submit"]');
        if (!(submitButton instanceof HTMLButtonElement)) {
            return;
        }

        submitButton.disabled = isBusy;
        submitButton.style.opacity = isBusy ? '0.7' : '1';
    };

    const updateLikeButton = (hasLiked, likeCount) => {
        if (!(likeButton instanceof HTMLButtonElement)) {
            return;
        }

        const likedLabel = likeButton.dataset.likedLabel || 'Da de xuat';
        const unlikedLabel = likeButton.dataset.unlikedLabel || 'De xuat';
        const icon = likeButton.querySelector('i');
        const nextLabel = hasLiked ? likedLabel : unlikedLabel;

        likeButton.classList.toggle('btn-primary', hasLiked);
        likeButton.classList.toggle('btn-outline', !hasLiked);
        likeButton.setAttribute('aria-pressed', hasLiked ? 'true' : 'false');
        if (icon instanceof HTMLElement) {
            icon.className = 'fa-solid fa-fire-flame-curved';
        }

        likeButton.innerHTML = `${icon instanceof HTMLElement ? icon.outerHTML : '<i class="fa-solid fa-fire-flame-curved"></i>'} ${nextLabel} (${likeCount})`;
    };

    const updateCommentCount = (count) => {
        if (commentCount instanceof HTMLElement) {
            commentCount.textContent = `${count}`;
        }
    };

    const updateCommentLikeButton = (commentId, hasLiked, likeCount) => {
        const button = detailRoot.querySelector(`[data-comment-id="${commentId}"] form[action*="ToggleCommentLike"] button[type="submit"]`);
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        button.classList.toggle('is-active', hasLiked);
        button.setAttribute('aria-pressed', hasLiked ? 'true' : 'false');
        button.title = 'Tim binh luan';
        button.innerHTML = `<i class="${hasLiked ? 'fa-solid' : 'fa-regular'} fa-heart"></i><span>${likeCount > 0 ? `Thich (${likeCount})` : 'Thich'}</span>`;
    };

    const notifyDynamicContentReady = (root) => {
        document.dispatchEvent(new CustomEvent('zenpace:emoji-refresh', {
            detail: { root }
        }));
        document.dispatchEvent(new CustomEvent('zenpace:presence-refresh', {
            detail: { root }
        }));
    };

    const applyRealtimeState = (stateElement) => {
        if (!(stateElement instanceof HTMLElement)) {
            return;
        }

        const likeCountValue = Number.parseInt(stateElement.dataset.likeCount || '', 10);
        const commentCountValue = Number.parseInt(stateElement.dataset.commentCount || '', 10);
        const hasLiked = stateElement.dataset.hasLiked === 'true';
        const commentFragment = stateElement.querySelector('[data-post-comment-list-fragment]');

        if (Number.isInteger(likeCountValue)) {
            updateLikeButton(hasLiked, likeCountValue);
        }

        if (Number.isInteger(commentCountValue)) {
            updateCommentCount(commentCountValue);
        }

        if (commentList instanceof HTMLElement && commentFragment instanceof HTMLElement) {
            commentList.innerHTML = commentFragment.innerHTML;
            notifyDynamicContentReady(commentList);
        }
    };

    const fetchRealtimeState = async () => {
        const response = await fetch(`/Post/RealtimeState/${postId}`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!response.ok) {
            throw new Error('Khong the dong bo lai bai viet luc nay.');
        }

        const html = await response.text();
        const template = document.createElement('template');
        template.innerHTML = html.trim();
        return template.content.querySelector('[data-post-realtime-state]');
    };

    const refreshPostState = async () => {
        if (refreshPromise) {
            refreshQueued = true;
            return refreshPromise;
        }

        refreshPromise = (async () => {
            const stateElement = await fetchRealtimeState();
            applyRealtimeState(stateElement);
        })();

        try {
            await refreshPromise;
        } finally {
            refreshPromise = null;
            if (refreshQueued) {
                refreshQueued = false;
                await refreshPostState();
            }
        }
    };

    const joinPostGroup = async () => {
        if (!connection || !isRealtimeAvailable() || joinedRealtime) {
            return;
        }

        await connection.invoke('JoinPost', postId);
        joinedRealtime = true;
    };

    if (connection) {
        connection.on('PostUpdated', (payload) => {
            const targetPostId = Number.parseInt(`${payload?.postId ?? ''}`, 10);
            if (targetPostId !== postId) {
                return;
            }

            refreshPostState().catch(() => {});
        });

        connection.onreconnected(() => {
            joinedRealtime = false;
            joinPostGroup()
                .then(() => refreshPostState())
                .catch(() => {});
        });
    }

    document.addEventListener('zenpace:social-realtime-changed', (event) => {
        const isReady = event instanceof CustomEvent && !!event.detail?.ready;
        if (!isReady) {
            joinedRealtime = false;
            return;
        }

        joinPostGroup().catch(() => {});
    });

    document.addEventListener('submit', async (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.classList.contains('ajax-postback')) {
            return;
        }

        event.preventDefault();
        setSubmitButtonBusy(form, true);

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            const data = await response.json().catch(() => null);
            if (!response.ok || !data?.success) {
                throw new Error(data?.message || 'Khong the xu ly yeu cau luc nay.');
            }

            if (form.action.includes('/Report')) {
                window.alert(data.message || 'Da gui bao cao.');
                form.reset();
                reportBox?.setAttribute('hidden', 'hidden');
                return;
            }

            if (form.action.includes('/ToggleLike') && Number.isInteger(Number.parseInt(`${data.likeCount ?? ''}`, 10))) {
                updateLikeButton(!!data.liked, Number.parseInt(`${data.likeCount}`, 10));
                return;
            }

            if (form.action.includes('/ToggleCommentLike') && Number.isInteger(Number.parseInt(`${data.commentId ?? ''}`, 10))) {
                updateCommentLikeButton(
                    Number.parseInt(`${data.commentId}`, 10),
                    !!data.liked,
                    Number.parseInt(`${data.likeCount ?? 0}`, 10)
                );
                return;
            }

            await refreshPostState();
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Khong the xu ly yeu cau luc nay.';
            window.alert(message);
        } finally {
            setSubmitButtonBusy(form, false);
        }
    });

    window.addEventListener('beforeunload', () => {
        if (!connection || !joinedRealtime) {
            return;
        }

        connection.send('LeavePost', postId).catch(() => {});
    });

    joinPostGroup().catch(() => {});
})();
