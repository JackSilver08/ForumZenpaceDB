(() => {
    const modal = document.querySelector('[data-story-modal]');
    if (!(modal instanceof HTMLElement)) {
        return;
    }

    const dialog = modal.querySelector('.story-modal-dialog');
    const form = modal.querySelector('[data-story-form]');
    const textInput = modal.querySelector('[data-story-text-input]');
    const backgroundInput = modal.querySelector('[data-story-background-input]');
    const imageInput = modal.querySelector('[data-story-image-input]');
    const imageName = modal.querySelector('[data-story-image-name]');
    const musicSelect = modal.querySelector('[data-story-music-select]');
    const musicLinkInput = modal.querySelector('[data-story-music-link-input]');
    const musicTitleInput = modal.querySelector('[data-story-music-title-input]');
    const musicArtistInput = modal.querySelector('[data-story-music-artist-input]');
    const musicUploadInput = modal.querySelector('[data-story-music-upload-input]');
    const trimControls = modal.querySelector('[data-story-music-trim-controls]');
    const musicName = modal.querySelector('[data-story-music-name]');
    const preview = modal.querySelector('[data-story-preview]');
    const previewCopy = modal.querySelector('[data-story-preview-copy]');
    const imagePreview = modal.querySelector('[data-story-image-preview]');
    const audioShell = modal.querySelector('[data-story-audio-shell]');
    const audioPreview = modal.querySelector('[data-story-audio-preview]');
    const audioLabel = modal.querySelector('[data-story-audio-label]');
    const audioCaption = modal.querySelector('[data-story-audio-caption]');
    const status = modal.querySelector('[data-story-form-status]');
    const submitButton = modal.querySelector('[data-story-submit]');
    let lastFocusedElement = null;
    let imagePreviewUrl = '';

    const escapeHtml = (value) => `${value ?? ''}`
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const getRequestVerificationToken = () => {
        const tokenMeta = document.querySelector('meta[name="request-verification-token"]');
        if (tokenMeta instanceof HTMLMetaElement && tokenMeta.content) {
            return tokenMeta.content;
        }

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput instanceof HTMLInputElement ? tokenInput.value : '';
    };

    const formatDateTime = (value) => {
        if (!value) {
            return '';
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return '';
        }

        return date.toLocaleString('vi-VN', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    const ensureToastStyle = () => {
        if (document.getElementById('storyToastStyles')) {
            return;
        }

        const style = document.createElement('style');
        style.id = 'storyToastStyles';
        style.textContent = `
            .story-toast-stack {
                position: fixed;
                right: 20px;
                bottom: 20px;
                z-index: 2000;
                display: flex;
                flex-direction: column;
                gap: 10px;
                pointer-events: none;
            }
            .story-toast {
                min-width: 260px;
                max-width: 360px;
                padding: 12px 14px;
                border-radius: 14px;
                background: rgba(14, 20, 34, 0.94);
                border: 1px solid rgba(255, 255, 255, 0.12);
                box-shadow: 0 18px 40px rgba(0, 0, 0, 0.28);
                color: rgba(255, 255, 255, 0.96);
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 12px;
                pointer-events: auto;
                backdrop-filter: blur(16px);
            }
            .story-toast__message {
                font-size: 0.92rem;
                line-height: 1.45;
            }
            .story-toast__action {
                flex-shrink: 0;
                color: #9bd0ff;
                font-weight: 700;
                text-decoration: none;
            }
            .story-toast__action:hover {
                color: #c5e4ff;
            }
            @media (max-width: 640px) {
                .story-toast-stack {
                    right: 12px;
                    bottom: 12px;
                    left: 12px;
                }
                .story-toast {
                    min-width: 0;
                    max-width: none;
                }
            }
        `;
        document.head.appendChild(style);
    };

    const showToast = (message, actionUrl = '', actionLabel = 'Xem ngay') => {
        ensureToastStyle();

        let stack = document.querySelector('.story-toast-stack');
        if (!(stack instanceof HTMLElement)) {
            stack = document.createElement('div');
            stack.className = 'story-toast-stack';
            document.body.appendChild(stack);
        }

        const toast = document.createElement('div');
        toast.className = 'story-toast';

        const messageNode = document.createElement('div');
        messageNode.className = 'story-toast__message';
        messageNode.textContent = message;
        toast.appendChild(messageNode);

        if (actionUrl) {
            const action = document.createElement('a');
            action.className = 'story-toast__action';
            action.href = actionUrl;
            action.textContent = actionLabel;
            toast.appendChild(action);
        }

        stack.appendChild(toast);
        window.setTimeout(() => {
            toast.remove();
            if (stack instanceof HTMLElement && stack.childElementCount === 0) {
                stack.remove();
            }
        }, 4200);
    };

    const setStatus = (message, isError = false) => {
        if (!(status instanceof HTMLElement)) {
            return;
        }

        if (!message) {
            status.hidden = true;
            status.textContent = '';
            status.classList.remove('is-error', 'is-success');
            return;
        }

        status.hidden = false;
        status.textContent = message;
        status.classList.toggle('is-error', isError);
        status.classList.toggle('is-success', !isError);
    };

    const resetComposer = () => {
        if (form instanceof HTMLFormElement) {
            form.reset();
        }

        if (trimControls instanceof HTMLElement) {
            trimControls.hidden = true;
        }

        setStatus('');
        updatePreviewBackground();
        updatePreviewCopy();
        updatePreviewImage();
        updatePreviewAudio();
    };

    const openModal = () => {
        lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        resetComposer();

        modal.hidden = false;
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('profile-modal-open');

        if (textInput instanceof HTMLTextAreaElement) {
            window.requestAnimationFrame(() => textInput.focus());
        }
    };

    const closeModal = () => {
        modal.hidden = true;
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('profile-modal-open');
        window.clearTimeout(status?._clearTimer);
        setStatus('');

        if (lastFocusedElement instanceof HTMLElement) {
            lastFocusedElement.focus();
        }

        lastFocusedElement = null;
    };

    const updatePreviewBackground = () => {
        if (!(preview instanceof HTMLElement) || !(backgroundInput instanceof HTMLSelectElement)) {
            return;
        }

        preview.classList.remove(
            'story-surface--aurora',
            'story-surface--sunset',
            'story-surface--lagoon',
            'story-surface--midnight'
        );

        preview.classList.add(`story-surface--${backgroundInput.value || 'aurora'}`);
    };

    const updatePreviewCopy = () => {
        if (!(previewCopy instanceof HTMLElement) || !(textInput instanceof HTMLTextAreaElement)) {
            return;
        }

        const value = textInput.value.trim();
        previewCopy.textContent = value || 'Nhap van ban hoac chon anh de xem truoc story.';
        previewCopy.classList.toggle('is-placeholder', value.length === 0);
    };

    const updatePreviewImage = () => {
        if (!(imagePreview instanceof HTMLImageElement) || !(imageInput instanceof HTMLInputElement)) {
            return;
        }

        if (imagePreviewUrl) {
            URL.revokeObjectURL(imagePreviewUrl);
            imagePreviewUrl = '';
        }

        const file = imageInput.files && imageInput.files[0];
        if (!file) {
            imagePreview.hidden = true;
            imagePreview.removeAttribute('src');
            if (imageName instanceof HTMLElement) {
                imageName.textContent = 'Chap nhan JPG, PNG, GIF, WEBP. Gioi han toi da 10MB.';
            }
            return;
        }

        imagePreviewUrl = URL.createObjectURL(file);
        imagePreview.src = imagePreviewUrl;
        imagePreview.hidden = false;
        if (imageName instanceof HTMLElement) {
            imageName.textContent = `Da chon: ${file.name}`;
        }
    };

    const updatePreviewAudio = () => {
        if (!(audioShell instanceof HTMLElement)
            || !(audioPreview instanceof HTMLAudioElement)
            || !(audioLabel instanceof HTMLElement)) {
            return;
        }

        const externalUrl = musicLinkInput instanceof HTMLInputElement
            ? musicLinkInput.value.trim()
            : '';
        const externalTitle = musicTitleInput instanceof HTMLInputElement
            ? musicTitleInput.value.trim()
            : '';
        const externalArtist = musicArtistInput instanceof HTMLInputElement
            ? musicArtistInput.value.trim()
            : '';

        if (musicSelect instanceof HTMLSelectElement) {
            musicSelect.disabled = externalUrl.length > 0;
        }

        const selectedOption = musicSelect instanceof HTMLSelectElement && musicSelect.selectedOptions.length > 0
            ? musicSelect.selectedOptions[0]
            : null;
        const trackKey = musicSelect instanceof HTMLSelectElement
            ? musicSelect.value.trim()
            : '';
        const libraryHint = musicName instanceof HTMLElement
            ? (musicName.dataset.storyMusicLibraryHint || 'Chon nhac co san tu thu vien chung.')
            : 'Chon nhac co san tu thu vien chung.';
        const emptyHint = musicName instanceof HTMLElement
            ? (musicName.dataset.storyMusicEmptyHint || 'Bo trong neu ban muon dang story khong kem nhac.')
            : 'Bo trong neu ban muon dang story khong kem nhac.';

        if (externalUrl) {
            audioShell.hidden = false;
            audioPreview.hidden = true;
            audioPreview.pause();
            audioPreview.removeAttribute('src');
            audioPreview.load();

            let sourceLabel = 'Link nhac';
            try {
                const parsedUrl = new URL(externalUrl);
                const host = parsedUrl.hostname.replace(/^www\./i, '').toLowerCase();
                if (host.includes('spotify.com')) {
                    sourceLabel = 'Spotify';
                } else if (host.includes('youtube.com') || host === 'youtu.be') {
                    sourceLabel = 'YouTube';
                }
            } catch {
                sourceLabel = 'Link nhac';
            }

            const composedLabel = externalTitle
                ? (externalArtist ? `${externalTitle} - ${externalArtist}` : externalTitle)
                : `${sourceLabel} link`;

            audioLabel.textContent = composedLabel;
            if (audioCaption instanceof HTMLElement) {
                audioCaption.textContent = `${sourceLabel} se hien trong thanh player mini o dau story.`;
            }
            if (musicName instanceof HTMLElement) {
                musicName.textContent = `${sourceLabel} link se duoc uu tien trong player mini cua story.`;
            }
            return;
        }

        if (audioCaption instanceof HTMLElement) {
            audioCaption.textContent = 'Nhac nay se di kem story khi xem.';
        }

        if (!trackKey || !(selectedOption instanceof HTMLOptionElement)) {
            audioShell.hidden = true;
            audioPreview.hidden = true;
            audioPreview.pause();
            audioPreview.removeAttribute('src');
            audioPreview.load();
            audioLabel.textContent = 'Chua chon nhac';
            if (musicName instanceof HTMLElement) {
                musicName.textContent = trackKey.length === 0 ? libraryHint : emptyHint;
            }
            return;
        }

        const trackLabel = (selectedOption.dataset.trackLabel || selectedOption.textContent || 'Nhac story').trim();
        const audioUrl = (selectedOption.dataset.trackAudioUrl || '').trim();
        if (!audioUrl) {
            audioShell.hidden = true;
            audioPreview.hidden = true;
            audioPreview.pause();
            audioPreview.removeAttribute('src');
            audioPreview.load();
            audioLabel.textContent = 'Chua chon nhac';
            if (musicName instanceof HTMLElement) {
                musicName.textContent = 'Bai nhac nay hien khong kha dung.';
            }
            return;
        }

        audioPreview.pause();
        audioPreview.src = audioUrl;
        audioPreview.load();
        audioPreview.hidden = false;
        audioShell.hidden = false;
        audioLabel.textContent = trackLabel;
        if (musicName instanceof HTMLElement) {
            musicName.textContent = `Da chon: ${trackLabel}`;
        }

        audioPreview.currentTime = 0;
        audioPreview.play().catch(() => {});
    };

    const renderOwnStoryCard = (currentUserStory, slot) => {
        const latestStoryId = Number.parseInt(`${currentUserStory?.latestStoryId ?? ''}`, 10);
        if (!Number.isInteger(latestStoryId) || latestStoryId <= 0) {
            return '';
        }

        const avatarUrl = slot.dataset.currentAvatarUrl || '';
        const currentUsername = slot.dataset.currentUsername || 'ban';
        const currentInitial = slot.dataset.currentUserInitial || 'B';
        const mediaClass = avatarUrl ? 'social-story-card__media has-cover' : 'social-story-card__media';
        const style = avatarUrl ? ` style="--story-cover-image: url('${escapeHtml(avatarUrl)}');"` : '';
        const avatarMarkup = avatarUrl
            ? `<img src="${escapeHtml(avatarUrl)}" alt="Story cua ban" />`
            : `<span>${escapeHtml(currentInitial)}</span>`;

        return `
            <a href="/Story/Viewer/${latestStoryId}"
               class="glass-panel social-card social-card--friend social-card--story social-card--story-self social-story-card social-story-card--fresh"
               data-own-story-card
               title="Xem story cua @${escapeHtml(currentUsername)}">
                <span class="${mediaClass}"${style}>
                    <span class="social-story-card__cover" aria-hidden="true"></span>
                    <span class="social-story-card__overlay" aria-hidden="true"></span>
                    <span class="social-story-pill">Ban</span>
                    <span class="social-story-card__avatar-frame social-story-card__avatar-frame--fresh">
                        <span class="social-avatar social-avatar--story is-own-story">${avatarMarkup}</span>
                    </span>
                    <span class="social-story-card__name" title="Story cua ban">Story cua ban</span>
                </span>
            </a>`;
    };

    const updateOwnStorySlot = (currentUserStory) => {
        const slot = document.querySelector('[data-own-story-slot]');
        if (!(slot instanceof HTMLElement)) {
            return;
        }

        slot.querySelector('[data-own-story-card]')?.remove();
        const createButton = slot.querySelector('[data-open-story-modal]');
        if (!(createButton instanceof HTMLElement)) {
            return;
        }

        const storyMarkup = renderOwnStoryCard(currentUserStory, slot);
        if (!storyMarkup) {
            return;
        }

        createButton.insertAdjacentHTML('afterend', storyMarkup);
    };

    const renderProfileStoryCard = (story) => {
        const hasImage = !!story?.hasImage && !!story?.imageUrl;
        const hasMusic = !!story?.hasMusic;
        const previewText = escapeHtml(story?.previewText || 'Khoanh khac moi');
        const backgroundStyle = escapeHtml(story?.backgroundStyle || 'aurora');
        const imageMarkup = hasImage
            ? `<img class="story-surface__image" src="${escapeHtml(story.imageUrl)}" alt="${previewText}" />`
            : '';
        const token = getRequestVerificationToken();
        const antiForgery = token
            ? `<input type="hidden" name="__RequestVerificationToken" value="${escapeHtml(token)}" />`
            : '';
        const returnUrl = `${window.location.pathname}${window.location.search}`;
        const statusPill = story?.hasBeenViewedByViewer
            ? ''
            : '<span class="status-pill is-live">Moi</span>';
        const musicPill = hasMusic
            ? '<span class="badge-tag"><i class="fa-solid fa-music"></i> Nhac</span>'
            : '';

        return `
            <article class="glass-card profile-story-card${story?.hasBeenViewedByViewer ? '' : ' profile-story-card--fresh'}" data-story-id="${escapeHtml(story?.id)}">
                <a href="/Story/Viewer/${escapeHtml(story?.id)}" class="profile-story-card-link">
                    <div class="profile-story-card-preview story-surface story-surface--${backgroundStyle}">
                        ${imageMarkup}
                        <div class="story-surface__overlay"></div>
                        <div class="story-surface__copy">${previewText}</div>
                    </div>
                    <div class="profile-story-card-body">
                        <div class="profile-story-card-meta">
                            <span>${escapeHtml(formatDateTime(story?.createdAt))}</span>
                            ${statusPill}
                            ${musicPill}
                        </div>
                        <p class="profile-story-card-caption">${previewText}</p>
                        <div class="profile-story-card-footer">
                            <span><i class="fa-regular fa-eye"></i> ${escapeHtml(story?.viewCount ?? 0)} luot xem</span>
                            <span><i class="fa-regular fa-clock"></i> Het han ${escapeHtml(formatDateTime(story?.expiresAt))}</span>
                        </div>
                    </div>
                </a>
                <form action="/Story/Delete" method="post" class="profile-story-delete">
                    ${antiForgery}
                    <input type="hidden" name="id" value="${escapeHtml(story?.id)}" />
                    <input type="hidden" name="returnUrl" value="${escapeHtml(returnUrl)}" />
                    <button type="submit" class="btn btn-text">Xoa</button>
                </form>
            </article>`;
    };

    const ensureActiveStorySection = (manager) => {
        let section = manager.querySelector('[data-profile-active-story-section]');
        if (section instanceof HTMLElement) {
            return section;
        }

        const emptyState = manager.querySelector('[data-profile-story-empty]');
        if (emptyState instanceof HTMLElement) {
            emptyState.remove();
        }

        const sectionMarkup = `
            <div class="profile-story-section" data-profile-active-story-section>
                <div class="profile-story-section-header">
                    <div>
                        <div class="panel-label">Dang hoat dong</div>
                        <h3 class="section-heading">Story trong 24 gio</h3>
                    </div>
                    <span class="status-pill is-live" data-profile-active-story-status>1 dang hien</span>
                </div>
                <div class="profile-story-grid" data-profile-active-story-grid></div>
            </div>`;

        const archivedSection = manager.querySelector('[data-profile-archived-story-section]');
        if (archivedSection instanceof HTMLElement) {
            archivedSection.insertAdjacentHTML('beforebegin', sectionMarkup);
            section = manager.querySelector('[data-profile-active-story-section]');
            return section instanceof HTMLElement ? section : null;
        }

        const cta = manager.querySelector('.profile-story-cta');
        if (cta instanceof HTMLElement) {
            cta.insertAdjacentHTML('afterend', sectionMarkup);
            section = manager.querySelector('[data-profile-active-story-section]');
            return section instanceof HTMLElement ? section : null;
        }

        manager.insertAdjacentHTML('beforeend', sectionMarkup);
        section = manager.querySelector('[data-profile-active-story-section]');
        return section instanceof HTMLElement ? section : null;
    };

    const updateProfileStoryManager = (payload) => {
        const manager = document.querySelector('[data-profile-story-manager][data-is-owner="true"]');
        if (!(manager instanceof HTMLElement) || !payload?.story) {
            return;
        }

        const story = payload.story;
        const currentUserStory = payload.currentUserStory || null;
        const section = ensureActiveStorySection(manager);
        if (!(section instanceof HTMLElement)) {
            return;
        }

        const grid = section.querySelector('[data-profile-active-story-grid]');
        if (!(grid instanceof HTMLElement)) {
            return;
        }

        const existing = grid.querySelector(`[data-story-id="${story.id}"]`);
        const storyMarkup = renderProfileStoryCard(story);
        const isNewStory = !(existing instanceof HTMLElement);

        if (existing instanceof HTMLElement) {
            existing.outerHTML = storyMarkup;
        } else {
            grid.insertAdjacentHTML('afterbegin', storyMarkup);
        }

        const activeCountValue = Number.parseInt(`${currentUserStory?.activeStoryCount ?? ''}`, 10);
        const activeCount = Number.isInteger(activeCountValue)
            ? activeCountValue
            : grid.querySelectorAll('[data-story-id]').length;

        const activeCountNode = manager.querySelector('[data-story-active-count]');
        if (activeCountNode instanceof HTMLElement) {
            activeCountNode.textContent = `${activeCount}`;
        }

        const totalCountNode = manager.querySelector('[data-story-total-count]');
        if (totalCountNode instanceof HTMLElement) {
            const currentTotal = Number.parseInt(totalCountNode.textContent || '0', 10);
            totalCountNode.textContent = `${isNewStory ? currentTotal + 1 : currentTotal}`;
        }

        const activeStatusNode = manager.querySelector('[data-profile-active-story-status]');
        if (activeStatusNode instanceof HTMLElement) {
            activeStatusNode.textContent = `${activeCount} dang hien`;
        }
    };

    const applyOwnStoryPublished = (payload, options = {}) => {
        if (!payload || !payload.story) {
            return;
        }

        updateOwnStorySlot(payload.currentUserStory || null);
        updateProfileStoryManager(payload);

        if (options.showToast) {
            showToast('Da dang story thanh cong.', payload.redirectUrl || '', 'Xem ngay');
        }
    };

    document.querySelectorAll('[data-open-story-modal]').forEach((button) => {
        button.addEventListener('click', () => openModal());
    });

    modal.querySelectorAll('[data-close-story-modal]').forEach((button) => {
        button.addEventListener('click', () => closeModal());
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && !modal.hidden) {
            closeModal();
        }
    });

    document.addEventListener('zenpace:own-story-published', (event) => {
        if (!(event instanceof CustomEvent)) {
            return;
        }

        applyOwnStoryPublished(event.detail, { showToast: false });
    });

    if (textInput instanceof HTMLTextAreaElement) {
        textInput.addEventListener('input', () => updatePreviewCopy());
    }

    if (backgroundInput instanceof HTMLSelectElement) {
        backgroundInput.addEventListener('change', () => updatePreviewBackground());
    }

    if (imageInput instanceof HTMLInputElement) {
        imageInput.addEventListener('change', () => updatePreviewImage());
    }

    if (musicSelect instanceof HTMLSelectElement) {
        musicSelect.addEventListener('change', () => updatePreviewAudio());
    }

    if (musicLinkInput instanceof HTMLInputElement) {
        musicLinkInput.addEventListener('input', () => updatePreviewAudio());
    }

    if (musicTitleInput instanceof HTMLInputElement) {
        musicTitleInput.addEventListener('input', () => updatePreviewAudio());
    }

    if (musicArtistInput instanceof HTMLInputElement) {
        musicArtistInput.addEventListener('input', () => updatePreviewAudio());
    }

    if (musicUploadInput instanceof HTMLInputElement) {
        musicUploadInput.addEventListener('change', () => {
            if (trimControls instanceof HTMLElement) {
                const hasFile = musicUploadInput.files && musicUploadInput.files.length > 0;
                trimControls.hidden = !hasFile;
            }
        });
    }

    if (form instanceof HTMLFormElement) {
        form.addEventListener('submit', async (event) => {
            event.preventDefault();

            if (!(submitButton instanceof HTMLButtonElement)) {
                return;
            }

            submitButton.disabled = true;
            setStatus('Dang dang story...');

            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: new FormData(form),
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                const data = await response.json().catch(() => null);
                if (!response.ok || !data?.success) {
                    setStatus(data?.message || 'Khong the dang story luc nay.', true);
                    submitButton.disabled = false;
                    return;
                }

                applyOwnStoryPublished(data, { showToast: true });
                resetComposer();
                closeModal();
                submitButton.disabled = false;
                return;
            } catch {
                setStatus('Khong the dang story luc nay.', true);
                submitButton.disabled = false;
            }
        });
    }

    updatePreviewBackground();
    updatePreviewCopy();
    updatePreviewAudio();
})();
