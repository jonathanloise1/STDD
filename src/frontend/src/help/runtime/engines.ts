/**
 * The registry of the application's answer engines (manual.md 7.1). A factory receives the status and the pack and
 * returns an `AnswerEngine` (see `engine.ts`), or null when the tier is not eligible. The order of the tiers comes
 * from the status, never from this file.
 */
import { browserEngineFactory } from '../assistant/browserEngine.ts';
import { createEngineRegistry } from './engine.ts';
import { serverEngineFactory } from './serverEngine.ts';

export const engineRegistry = createEngineRegistry();

engineRegistry.register('browser', browserEngineFactory);
engineRegistry.register('server', serverEngineFactory);
