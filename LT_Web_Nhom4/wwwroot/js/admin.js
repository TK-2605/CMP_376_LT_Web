(() => {
  document.querySelectorAll('[data-admin-confirm]').forEach((element) => {
    element.addEventListener('click', (event) => {
      const message = element.getAttribute('data-admin-confirm') || 'Ban chac chan muon thuc hien thao tac nay?';
      if (!window.confirm(message)) {
        event.preventDefault();
      }
    });
  });
})();
