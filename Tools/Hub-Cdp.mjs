import fs from 'node:fs';

const targets = await (await fetch('http://127.0.0.1:9333/json/list')).json();
const target = targets.find(t => t.type === 'page' && t.title === 'Unity Hub');
if (!target) throw new Error('Unity Hub debugging target is unavailable');
const socket = new WebSocket(target.webSocketDebuggerUrl);
const result = await new Promise((resolve, reject) => {
  const timeout = setTimeout(() => reject(new Error('Hub operation timed out')), 15000);
  socket.onopen = () => socket.send(JSON.stringify({
    id: 1,
    method: process.argv[2] === '--screenshot' ? 'Page.captureScreenshot' : 'Runtime.evaluate',
    params: process.argv[2] === '--screenshot'
      ? { format: 'png' }
      : { expression: process.argv[2] || 'document.body.innerText', returnByValue: true, awaitPromise: true }
  }));
  socket.onmessage = event => {
    const message = JSON.parse(event.data);
    if (message.id !== 1) return;
    clearTimeout(timeout);
    socket.close();
    if (message.error || message.result.exceptionDetails)
      reject(new Error(JSON.stringify(message.error || message.result.exceptionDetails)));
    else resolve(message.result);
  };
  socket.onerror = reject;
});
if (process.argv[2] === '--screenshot') {
  fs.writeFileSync(process.argv[3], Buffer.from(result.data, 'base64'));
  console.log(process.argv[3]);
} else console.log(result.result.value ?? result.result.description ?? '');
