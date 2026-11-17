---
schema_version: 1
kind: story
lang: en
page_id: authentication/US-AUTH-09
source: docs/areas/authentication/user-stories/US-AUTH-09.md
source_hash: 70a72d2182224100fdd5b41a587bc27543826cbf
steps_from: AUTH-SHORTCUT-001
steps_hash: cf16399877e065f519fec2cc91a7535c01770022
routes: [/dashboard]
contexts: []
screenshots: [images/shortcuts-dialog-open.png, images/shortcuts-dialog-open-as-viewer.png]
---

# Move between pages with the keyboard

Open the main pages with a few key presses, without the mouse.

## Steps

1. Press **G** and then **D** to open the Dashboard page, or **G** and then **T** to open the Tasks page.
2. As an Admin, press **G** and then **M** for the Team page, **G** and then **A** for the Audit Log page, or **G** and then **S** for the Organization page.
3. Press **?** to open the "Keyboard Shortcuts" dialog. It lists only the shortcuts you can use.

   ![The Keyboard Shortcuts dialog as an Admin sees it, with the navigation shortcuts](images/shortcuts-dialog-open.png)

4. As an Editor or a Viewer, the dialog lists only the Dashboard and Tasks shortcuts.

   ![The Keyboard Shortcuts dialog as a Viewer sees it, with only Dashboard and Tasks](images/shortcuts-dialog-open-as-viewer.png)

## What you will see

- The page of the shortcut you pressed opens.

## Good to know

- Editors and Viewers have only the shortcuts of the Dashboard and Tasks pages: G followed by M, A or S does nothing for them.
- Shortcuts do nothing while you type in a field or while a dialog is open: keys typed in a field are text.
- You cannot choose other keys, and there are no shortcuts for actions inside a page, such as creating a task.

## Related pages

- [Move between the sections of the app](US-AUTH-06.md)
- [Open only the pages your role allows](US-AUTH-07.md)
