const ts = require('typescript');
const fs = require('fs');
const path = require('path');

function compile(tsconfigPath){
  const configText = fs.readFileSync(tsconfigPath, 'utf-8');
  const result = ts.parseConfigFileTextToJson(tsconfigPath, configText);
  const configParseResult = ts.parseJsonConfigFileContent(result.config, ts.sys, path.dirname(tsconfigPath));
  const program = ts.createProgram({ rootNames: configParseResult.fileNames, options: configParseResult.options });
  const emitResult = program.emit();
  const diagnostics = ts.getPreEmitDiagnostics(program).concat(emitResult.diagnostics);
  const log = diagnostics.map(d => ts.flattenDiagnosticMessageText(d.messageText, '\n') + (d.file ? ` @ ${d.file.fileName}:${d.start}` : '') ).join('\n');
  return { ok: diagnostics.length === 0, log };
}

const out = compile(path.resolve(__dirname, '..', 'tsconfig.json'));
fs.writeFileSync(path.resolve(__dirname, '..', 'tsbuild.log'), out.log, 'utf-8');
console.log(out.ok ? 'OK' : 'ERRORS');
