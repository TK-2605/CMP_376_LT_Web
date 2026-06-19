(() => {
  const form = document.querySelector('[data-exam-form]');
  if (!form) {
    return;
  }

  const durationMinutes = Number(form.dataset.durationMinutes || '0');
  const startedAt = new Date(form.dataset.startedAt || new Date().toISOString()).getTime();
  const totalSeconds = Math.max(durationMinutes * 60, 1);
  const timerLabel = document.querySelector('[data-exam-timer-label]');
  const timerBar = document.querySelector('[data-exam-timer-bar]');
  const autosaveLabel = document.querySelector('[data-exam-autosave]');
  const antiCheatStatus = document.querySelector('[data-anti-cheat-status]');
  let isSubmitting = false;
  let autosaveTimer = null;
  let lastViolationAt = 0;

  const reportViolation = async (eventType, note) => {
    const url = form.dataset.antiCheatUrl;
    const now = Date.now();
    if (!url || isSubmitting || now - lastViolationAt < 800) {
      return;
    }

    lastViolationAt = now;
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;

    try {
      const response = await fetch(url, {
        method: 'POST',
        credentials: 'same-origin',
        keepalive: true,
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': token || '',
          'X-Requested-With': 'XMLHttpRequest'
        },
        body: JSON.stringify({
          examId: Number(form.querySelector('input[name="ExamId"]')?.value || '0'),
          examAttemptId: Number(form.querySelector('input[name="AttemptId"]')?.value || '0'),
          eventType,
          note
        })
      });

      if (!response.ok) {
        return;
      }

      const result = await response.json();
      if (antiCheatStatus) {
        const template = result.isSuspicious
          ? form.dataset.antiCheatSuspicious
          : form.dataset.antiCheatWarning;
        antiCheatStatus.textContent = (template || '').replace('{0}', result.violationCount);
        antiCheatStatus.classList.remove('d-none', 'alert-info', 'alert-danger');
        antiCheatStatus.classList.add(result.isSuspicious ? 'alert-danger' : 'alert-info');
      }
    } catch {
      // The exam flow must continue even if a monitoring request is interrupted.
    }
  };

  document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
      reportViolation(1, 'The exam page became hidden or the browser was minimized.');
    }
  });

  window.addEventListener('blur', () => {
    if (!document.hidden) {
      reportViolation(2, 'The exam window lost focus.');
    }
  });

  window.addEventListener('focus', () => {
    if (antiCheatStatus && antiCheatStatus.textContent) {
      antiCheatStatus.setAttribute('aria-live', 'polite');
    }
  });

  const updateProgressDots = () => {
    document.querySelectorAll('[data-question-id]').forEach((questionElement) => {
      const questionId = questionElement.getAttribute('data-question-id');
      const dot = document.querySelector(`[data-progress-question="${questionId}"]`);
      const answered = questionElement.querySelector('input[type="radio"]:checked');
      if (dot) {
        dot.classList.toggle('is-answered', Boolean(answered));
      }
    });
  };

  const updateTimer = () => {
    const elapsedSeconds = Math.floor((Date.now() - startedAt) / 1000);
    const remaining = Math.max(totalSeconds - elapsedSeconds, 0);
    const minutes = Math.floor(remaining / 60).toString().padStart(2, '0');
    const seconds = (remaining % 60).toString().padStart(2, '0');
    const percent = (remaining / totalSeconds) * 100;

    if (timerLabel) {
      timerLabel.textContent = `${minutes}:${seconds}`;
    }

    if (timerBar) {
      timerBar.style.width = `${percent}%`;
      timerBar.classList.toggle('is-warning', remaining <= 60);
    }

    if (remaining === 0 && !isSubmitting) {
      isSubmitting = true;
      form.submit();
    }
  };

  const autosave = async () => {
    const url = form.dataset.autosaveUrl;
    if (!url || isSubmitting) {
      return;
    }

    try {
      const response = await fetch(url, {
        method: 'POST',
        body: new FormData(form),
        headers: {
          'X-Requested-With': 'XMLHttpRequest'
        }
      });

      if (autosaveLabel) {
        if (response.ok) {
          const data = await response.json();
          autosaveLabel.textContent = (form.dataset.savedAt || 'Saved at {0}').replace('{0}', data.savedAt);
          autosaveLabel.classList.remove('text-danger');
          autosaveLabel.classList.add('text-muted');
        } else {
          autosaveLabel.textContent = form.dataset.autosaveFailed || 'Could not save yet.';
          autosaveLabel.classList.add('text-danger');
        }
      }
    } catch {
      if (autosaveLabel) {
        autosaveLabel.textContent = form.dataset.connectionUnstable || 'Connection is unstable.';
        autosaveLabel.classList.add('text-danger');
      }
    }
  };

  form.addEventListener('change', (event) => {
    if (event.target.matches('input[type="radio"]')) {
      updateProgressDots();
      window.clearTimeout(autosaveTimer);
      autosaveTimer = window.setTimeout(autosave, 500);
    }
  });

  form.addEventListener('submit', (event) => {
    if (isSubmitting) {
      return;
    }

    const totalQuestions = document.querySelectorAll('[data-question-id]').length;
    const answeredQuestions = document.querySelectorAll('[data-question-id] input[type="radio"]:checked').length;
    const unansweredCount = totalQuestions - answeredQuestions;

    const unansweredMessage = (form.dataset.unansweredConfirm || 'You have {0} unanswered questions. Submit anyway?')
      .replace('{0}', unansweredCount);
    if (unansweredCount > 0 && !window.confirm(unansweredMessage)) {
      event.preventDefault();
      return;
    }

    isSubmitting = true;
    form.querySelectorAll('button[type="submit"]').forEach((button) => {
      button.disabled = true;
      button.textContent = form.dataset.submitting || 'Submitting...';
    });
  });

  updateProgressDots();
  updateTimer();
  window.setInterval(updateTimer, 1000);
})();
