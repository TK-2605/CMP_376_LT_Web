(() => {
  document.querySelectorAll('[data-admin-confirm]').forEach((element) => {
    element.addEventListener('click', (event) => {
      const message = element.getAttribute('data-admin-confirm') || 'Ban chac chan muon thuc hien thao tac nay?';
      if (!window.confirm(message)) {
        event.preventDefault();
      }
    });
  });

  document.querySelectorAll('[data-admin-table-filter]').forEach((input) => {
    const table = input.closest('.admin-crud-page')?.querySelector('[data-admin-table-body]');
    if (!table) {
      return;
    }

    input.addEventListener('input', () => {
      const keyword = input.value.trim().toLowerCase();
      table.querySelectorAll('[data-admin-table-row]').forEach((row) => {
        row.hidden = keyword.length > 0 && !row.innerText.toLowerCase().includes(keyword);
      });
    });
  });
})();
