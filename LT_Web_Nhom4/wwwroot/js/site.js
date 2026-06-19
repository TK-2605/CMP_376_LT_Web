(() => {
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

  document.addEventListener('click', (event) => {
    const target = event.target.closest('[data-confirm]');
    if (target && !window.confirm(target.dataset.confirm)) {
      event.preventDefault();
    }
  });
})();
