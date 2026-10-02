const http = require("http");

function getJson(url) {
  return new Promise((resolve, reject) => {
    http
      .get(url, (response) => {
        let data = "";
        response.on("data", (chunk) => {
          data += chunk;
        });
        response.on("end", () => resolve(JSON.parse(data)));
      })
      .on("error", reject);
  });
}

async function main() {
  const pages = await getJson("http://127.0.0.1:9222/json/list");
  const page = pages.find((item) => item.url === "http://127.0.0.1:4173/");
  if (!page) throw new Error("Warehouse Lockdown page not found");

  const socket = new WebSocket(page.webSocketDebuggerUrl);
  const errors = [];
  let nextId = 0;

  function send(method, params = {}) {
    return new Promise((resolve, reject) => {
      const id = ++nextId;
      function onMessage(event) {
        const message = JSON.parse(event.data);
        if (message.method === "Runtime.exceptionThrown") {
          const description = message.params.exceptionDetails?.exception?.description || "";
          if (!description.includes("Pointer Lock")) errors.push(message.params);
        }
        if (message.method === "Log.entryAdded" && ["error", "violation"].includes(message.params.entry.level)) {
          const url = message.params.entry.url || "";
          if (!url.endsWith("/favicon.ico")) errors.push(message.params);
        }
        if (message.id === id) {
          socket.removeEventListener("message", onMessage);
          if (message.error) reject(new Error(JSON.stringify(message.error)));
          else resolve(message.result);
        }
      }
      socket.addEventListener("message", onMessage);
      socket.send(JSON.stringify({ id, method, params }));
    });
  }

  await new Promise((resolve) => socket.addEventListener("open", resolve, { once: true }));
  await send("Runtime.enable");
  await send("Log.enable");
  await send("Page.bringToFront");
  await send("Page.reload", { ignoreCache: true });
  await new Promise((resolve) => setTimeout(resolve, 900));
  await send("Runtime.evaluate", {
    expression: "document.querySelector('#startButton').click(); true",
    returnByValue: true
  });
  await send("Input.dispatchKeyEvent", { type: "keyDown", code: "KeyW", key: "w", windowsVirtualKeyCode: 87 });
  await new Promise((resolve) => setTimeout(resolve, 650));
  await send("Input.dispatchKeyEvent", { type: "keyUp", code: "KeyW", key: "w", windowsVirtualKeyCode: 87 });
  const state = await send("Runtime.evaluate", {
    expression:
      "({panelHidden:!document.querySelector('#centerPanel').classList.contains('show'),hud:document.querySelector('#terminalText').textContent,canvas:!!document.querySelector('canvas'),prompt:document.querySelector('#prompt').textContent})",
    returnByValue: true
  });
  await send("Input.dispatchKeyEvent", { type: "keyDown", code: "Escape", key: "Escape", windowsVirtualKeyCode: 27 });
  await new Promise((resolve) => setTimeout(resolve, 100));
  const paused = await send("Runtime.evaluate", {
    expression: "document.querySelector('#pausePanel').classList.contains('show')",
    returnByValue: true
  });

  console.log(
    JSON.stringify(
      {
        state: state.result.value,
        paused: paused.result.value,
        errorCount: errors.length,
        errors: errors.slice(0, 3)
      },
      null,
      2
    )
  );
  socket.close();

  if (errors.length) process.exit(1);
  if (!state.result.value.panelHidden || !state.result.value.canvas || !paused.result.value) process.exit(1);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
