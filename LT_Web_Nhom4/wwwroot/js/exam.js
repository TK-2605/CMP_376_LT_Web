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
  let isSubmitting = false;
  let autosaveTimer = null;

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
          autosaveLabel.textContent = `Da luu luc ${data.savedAt}`;
          autosaveLabel.classList.remove('text-danger');
          autosaveLabel.classList.add('text-muted');
        } else {
          autosaveLabel.textContent = 'Chua luu duoc. Lua chon cua ban van dang hien tren man hinh.';
          autosaveLabel.classList.add('text-danger');
        }
      }
    } catch {
      if (autosaveLabel) {
        autosaveLabel.textContent = 'Ket noi chua on dinh. He thong se thu luu lai.';
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

    if (unansweredCount > 0 && !window.confirm(`Ban con ${unansweredCount} cau chua tra loi. Van nop bai?`)) {
      event.preventDefault();
      return;
    }

    isSubmitting = true;
    form.querySelectorAll('button[type="submit"]').forEach((button) => {
      button.disabled = true;
      button.textContent = 'Dang nop bai...';
    });
  });

  updateProgressDots();
  updateTimer();
  window.setInterval(updateTimer, 1000);
})();
