(() => {
  'use strict';

  const escapeHtml = (value) => String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');

  const header = document.querySelector('[data-site-header]');
  if (header) {
    const updateHeader = () => header.classList.toggle('is-scrolled', window.scrollY > 24);
    window.addEventListener('scroll', updateHeader, { passive: true });
    updateHeader();
  }

  document.querySelectorAll('[data-auth-toast]').forEach((toast) => {
    const delay = Number(toast.dataset.autoDismiss || 0);
    if (!delay) return;
    window.setTimeout(() => {
      toast.classList.add('is-hiding');
      window.setTimeout(() => toast.remove(), 250);
    }, delay);
  });

  document.querySelectorAll('[data-class-code]').forEach((input) => {
    const format = () => {
      const raw = input.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 9);
      input.value = [raw.slice(0, 3), raw.slice(3, 6), raw.slice(6, 9)].filter(Boolean).join('-');
    };
    input.addEventListener('input', format);
    format();
  });

  document.querySelectorAll('[data-class-filter]').forEach((input) => {
    input.addEventListener('input', () => {
      const term = input.value.trim().toLocaleLowerCase('vi');
      document.querySelectorAll('[data-class-card]').forEach((card) => {
        card.hidden = term.length > 0 && !card.dataset.searchText.includes(term);
      });
    });
  });

  document.querySelectorAll('[data-hero-slider]').forEach((slider) => {
    const slides = Array.from(slider.querySelectorAll('[data-hero-slide]'));
    const dots = Array.from(slider.querySelectorAll('[data-hero-dot]'));
    if (slides.length < 2) return;

    let index = 0;
    let intervalId;
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const render = (nextIndex) => {
      index = (nextIndex + slides.length) % slides.length;
      slides.forEach((slide, slideIndex) => {
        const active = slideIndex === index;
        slide.classList.toggle('is-active', active);
        slide.setAttribute('aria-hidden', active ? 'false' : 'true');
      });
      dots.forEach((dot, dotIndex) => {
        const active = dotIndex === index;
        dot.classList.toggle('is-active', active);
        dot.setAttribute('aria-selected', active ? 'true' : 'false');
      });
    };

    const stop = () => {
      if (intervalId) window.clearInterval(intervalId);
      intervalId = undefined;
    };
    const start = () => {
      stop();
      if (!reducedMotion) intervalId = window.setInterval(() => render(index + 1), 6500);
    };

    slider.querySelector('[data-hero-previous]')?.addEventListener('click', () => { render(index - 1); start(); });
    slider.querySelector('[data-hero-next]')?.addEventListener('click', () => { render(index + 1); start(); });
    dots.forEach((dot) => dot.addEventListener('click', () => { render(Number(dot.dataset.heroDot)); start(); }));
    slider.addEventListener('mouseenter', stop);
    slider.addEventListener('mouseleave', start);
    slider.addEventListener('focusin', stop);
    slider.addEventListener('focusout', start);
    slider.addEventListener('keydown', (event) => {
      if (event.key === 'ArrowLeft') render(index - 1);
      if (event.key === 'ArrowRight') render(index + 1);
    });

    render(0);
    start();
  });

  const revealItems = document.querySelectorAll('[data-reveal]');
  if ('IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    const revealObserver = new IntersectionObserver((entries, observer) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-visible');
        observer.unobserve(entry.target);
      });
    }, { threshold: 0.12 });
    revealItems.forEach((item) => revealObserver.observe(item));
  } else {
    revealItems.forEach((item) => item.classList.add('is-visible'));
  }

  document.querySelectorAll('[data-autocomplete-root]').forEach((root) => {
    const input = root.querySelector('[data-autocomplete-input]');
    const results = root.querySelector('[data-autocomplete-results]');
    if (!input || !results) return;

    let timerId;
    let requestController;
    let activeIndex = -1;

    const close = () => {
      results.hidden = true;
      results.innerHTML = '';
      activeIndex = -1;
    };

    const setActive = (nextIndex) => {
      const links = Array.from(results.querySelectorAll('a'));
      if (links.length === 0) return;
      activeIndex = (nextIndex + links.length) % links.length;
      links.forEach((link, linkIndex) => link.classList.toggle('is-active', linkIndex === activeIndex));
    };

    const search = async () => {
      const term = input.value.trim();
      if (term.length < 2) {
        close();
        return;
      }

      requestController?.abort();
      requestController = new AbortController();
      try {
        const response = await fetch(`${root.dataset.searchUrl}?q=${encodeURIComponent(term)}`, {
          headers: { 'X-Requested-With': 'XMLHttpRequest' },
          signal: requestController.signal
        });
        if (!response.ok) throw new Error('Không thể tìm kiếm');
        const items = await response.json();
        if (items.length === 0) {
          results.innerHTML = '<div class="px-3 py-2 text-muted small">Không tìm thấy lớp hoặc đề phù hợp.</div>';
        } else {
          results.innerHTML = items.map((item) => `<a href="${escapeHtml(item.url)}"><i class="${item.type === 'class' ? 'ri-graduation-cap-line' : 'ri-file-list-3-line'}"></i><span><strong>${escapeHtml(item.title)}</strong><small>${escapeHtml(item.meta)}</small></span></a>`).join('');
        }
        results.hidden = false;
        activeIndex = -1;
      } catch (error) {
        if (error.name !== 'AbortError') close();
      }
    };

    input.addEventListener('input', () => {
      window.clearTimeout(timerId);
      timerId = window.setTimeout(search, 220);
    });
    input.addEventListener('keydown', (event) => {
      const links = Array.from(results.querySelectorAll('a'));
      if (event.key === 'ArrowDown' && links.length) { event.preventDefault(); setActive(activeIndex + 1); }
      if (event.key === 'ArrowUp' && links.length) { event.preventDefault(); setActive(activeIndex - 1); }
      if (event.key === 'Enter' && activeIndex >= 0 && links[activeIndex]) { event.preventDefault(); links[activeIndex].click(); }
      if (event.key === 'Escape') close();
    });
    document.addEventListener('click', (event) => { if (!root.contains(event.target)) close(); });
  });

  document.querySelectorAll('[data-image-input]').forEach((input) => {
    const target = document.querySelector(input.dataset.imageInput);
    if (!target) return;
    input.addEventListener('change', () => {
      const file = input.files?.[0];
      if (!file || !file.type.startsWith('image/')) return;
      const objectUrl = URL.createObjectURL(file);
      target.src = objectUrl;
      target.hidden = false;
      target.onload = () => URL.revokeObjectURL(objectUrl);
    });
  });

  document.querySelectorAll('[data-new-subject-toggle]').forEach((toggle) => {
    const form = toggle.closest('form');
    const fields = form?.querySelector('[data-new-subject-fields]');
    const select = form?.querySelector('[data-existing-subject]');
    const update = () => {
      const enabled = toggle.checked;
      if (fields) fields.hidden = !enabled;
      if (select) select.disabled = enabled;
      fields?.querySelectorAll('input, textarea').forEach((field) => {
        if (field.name.includes('NewSubjectCode') || field.name.includes('NewSubjectName')) {
          field.required = enabled;
        }
      });
    };
    toggle.addEventListener('change', update);
    update();
  });

  document.querySelectorAll('[data-subject-code-input]').forEach((input) => {
    const normalize = () => {
      input.value = input.value.toUpperCase().replace(/[^A-Z0-9_-]/g, '').slice(0, 50);
    };
    input.addEventListener('input', normalize);
    normalize();
  });

  document.querySelectorAll('[data-media-preview]').forEach((input) => {
    const target = document.querySelector(input.dataset.mediaPreview);
    if (!target) return;
    input.addEventListener('change', () => {
      (target._mediaPreviewUrls || []).forEach((url) => URL.revokeObjectURL(url));
      target._mediaPreviewUrls = [];
      target.innerHTML = '';
      const files = Array.from(input.files || []);
      target.hidden = files.length === 0;
      files.forEach((file) => {
        const item = document.createElement('div');
        item.className = 'selected-media-item';
        const objectUrl = URL.createObjectURL(file);
        target._mediaPreviewUrls.push(objectUrl);
        if (file.type.startsWith('image/')) {
          const img = document.createElement('img');
          img.src = objectUrl;
          img.alt = file.name;
          img.loading = 'lazy';
          item.append(img);
        } else if (file.type.startsWith('video/')) {
          const video = document.createElement('video');
          video.src = objectUrl;
          video.controls = true;
          video.preload = 'metadata';
          item.append(video);
        }
        const label = document.createElement('span');
        label.textContent = file.name;
        item.append(label);
        target.append(item);
      });
    });
  });

  window.addEventListener('pagehide', () => {
    document.querySelectorAll('[data-media-preview]').forEach((input) => {
      const target = document.querySelector(input.dataset.mediaPreview);
      (target?._mediaPreviewUrls || []).forEach((url) => URL.revokeObjectURL(url));
    });
  });

  document.querySelectorAll('[data-ajax-form="remove-member"]').forEach((form) => {
    form.addEventListener('submit', async (event) => {
      event.preventDefault();

      const row = form.closest('[data-member-row]');
      const button = form.querySelector('button[type="submit"]');
      const messageBox = document.querySelector('[data-class-ajax-message]');
      button?.setAttribute('disabled', 'disabled');

      const showMessage = (message, isError) => {
        if (!messageBox) return;
        messageBox.textContent = message;
        messageBox.classList.toggle('d-none', false);
        messageBox.classList.toggle('alert-success', !isError);
        messageBox.classList.toggle('alert-danger', Boolean(isError));
      };

      const updateMemberCount = (count) => {
        document.querySelectorAll('[data-member-count]').forEach((item) => {
          item.textContent = String(count);
        });

        const heroCount = document.querySelector('.class-detail-meta .ri-group-line')?.closest('span');
        if (heroCount && !heroCount.querySelector('[data-member-count]')) {
          heroCount.innerHTML = `<i class="ri-group-line"></i> ${count} học viên`;
        }

        const memberHeading = document.querySelector('aside .surface-heading h2');
        if (memberHeading && !memberHeading.querySelector('[data-member-count]')) {
          memberHeading.textContent = `${count} học viên`;
        }
      };

      try {
        const response = await fetch(form.action || window.location.href, {
          method: 'POST',
          body: new FormData(form),
          headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        const data = await response.json().catch(() => ({}));
        if (!response.ok || data.ok === false) throw new Error(data.message || 'Không thể xóa học viên.');

        row?.remove();
        if (typeof data.memberCount === 'number') updateMemberCount(data.memberCount);
        showMessage(data.message || 'Học viên đã được xóa khỏi lớp.', false);
      } catch (error) {
        showMessage(error.message || 'Không thể xóa học viên. Vui lòng thử lại.', true);
      } finally {
        button?.removeAttribute('disabled');
      }
    });
  });

  document.querySelectorAll('[data-tabs]').forEach((tabs) => {
    const buttons = Array.from(tabs.querySelectorAll('[data-tab-target]'));
    const panels = Array.from(document.querySelectorAll('[data-tab-panel]'));
    const activate = (name, updateHash = true) => {
      buttons.forEach((button) => {
        const active = button.dataset.tabTarget === name;
        button.classList.toggle('is-active', active);
        button.setAttribute('aria-selected', active ? 'true' : 'false');
      });
      panels.forEach((panel) => { panel.hidden = panel.dataset.tabPanel !== name; });
      if (updateHash) history.replaceState(null, '', `#${name}`);
    };
    buttons.forEach((button) => button.addEventListener('click', () => activate(button.dataset.tabTarget)));
    const requested = window.location.hash.slice(1);
    activate(buttons.some((button) => button.dataset.tabTarget === requested) ? requested : buttons[0]?.dataset.tabTarget, false);
  });

  const modalElement = document.getElementById('confirmActionModal');
  const modal = modalElement && window.bootstrap ? new bootstrap.Modal(modalElement) : null;
  let pendingTarget;

  document.addEventListener('click', (event) => {
    const target = event.target.closest('[data-confirm]');
    if (!target || target.dataset.confirmBypass === 'true') return;
    event.preventDefault();
    if (!modal) {
      if (window.confirm(target.dataset.confirm)) executeConfirmed(target);
      return;
    }
    pendingTarget = target;
    modalElement.querySelector('[data-confirm-message]').textContent = target.dataset.confirm;
    modal.show();
  });

  const executeConfirmed = (target) => {
    if (target.tagName === 'A' && target.href) {
      window.location.assign(target.href);
      return;
    }
    if (target.form) {
      target.dataset.confirmBypass = 'true';
      target.form.requestSubmit(target);
      window.setTimeout(() => { delete target.dataset.confirmBypass; }, 0);
    }
  };

  modalElement?.querySelector('[data-confirm-accept]')?.addEventListener('click', () => {
    if (!pendingTarget) return;
    const target = pendingTarget;
    pendingTarget = undefined;
    modal.hide();
    executeConfirmed(target);
  });
})();
