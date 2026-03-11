# Cursor MCP for Karawan

## Notebook MCP

This project is configured to use the **notebook-mcp** server (see `mcp.json`), which exposes tools such as:

- `notebook_search` — search notebook entries (e.g. for "Karawan architecture")
- `notebook_browse` — browse catalog by topic
- `notebook_read` — read a specific entry
- `notebook_get_context` — purpose, open questions, catalog summary
- `notebook_write`, `notebook_revise`, etc.

### Verified working

- **Script**: `C:\Users\weggen\.cyber\notebook_mcp.py` runs correctly.
- **API**: `https://notebook.nassau-records.de` is reachable; `notebook_get_context` returns the Karawan notebook catalog (100+ entries).
- **Config**: `.cursor/mcp.json` matches your global `%USERPROFILE%\.cursor\mcp.json` (same URL, notebook ID, token, script path).

### Getting the MCP to load in Cursor

1. **Open the Karawan project as the workspace root**  
   File → Open Folder → select the `Karawan` repo folder. Cursor loads project MCPs from the opened folder’s `.cursor/mcp.json`.

2. **Restart Cursor fully**  
   MCP servers are only started at Cursor launch. After adding or changing `.cursor/mcp.json`:
   - Quit Cursor completely (not just the window).
   - Start Cursor again and open the Karawan folder.

3. **Use a new chat**  
   Start a new chat in this workspace; the notebook tools should appear when the AI uses MCP (e.g. “Search the notebook for …”).

### If tools still don’t appear

- **Cursor Settings → MCP**: Check that `notebook-mcp` is listed and has no error. If it failed to start, you’ll see a message there.
- **Python**: Cursor runs `python` from the environment it uses for the integrated terminal. Ensure `python` is on PATH when Cursor starts (e.g. from the same shell/profile you use for development).
- **Token expiry**: The JWT in `mcp.json` may expire. If the API returns 401, generate a new token from the notebook admin panel and update `NOTEBOOK_TOKEN` in `.cursor/mcp.json` (and in `%USERPROFILE%\.cursor\mcp.json` if you rely on global config).

### Security

- `.cursor/mcp.json` is in `.gitignore` so the token is not committed.
