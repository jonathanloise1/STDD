import React, { FC, ReactNode } from 'react';

// Stub: original Chat component was removed during marketplace cleanup.
// This placeholder keeps Facit template pages compilable.

interface StubProps {
	[key: string]: any;
	children?: ReactNode;
}

const Chat: FC<StubProps> = ({ children }) => <div className='chat-placeholder'>{children}</div>;

export const ChatGroup: FC<StubProps> = () => null;
export const ChatHeader: FC<StubProps> = () => null;
export const ChatAvatar: FC<StubProps> = () => null;
export const ChatListItem: FC<StubProps> = () => null;

export default Chat;
