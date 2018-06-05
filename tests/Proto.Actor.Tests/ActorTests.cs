﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class ActorTests
    {
        public static PID SpawnActorFromFunc(Receive receive) => Actor.Spawn(Actor.FromFunc(receive));


        [Fact]
        public async Task RequestActorAsync()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            });

            var reply = await RootContext.Empty.RequestAsync<object>(pid, "hello");

            Assert.Equal("hey", reply);
        }

        [Fact]
        public async Task RequestActorAsync_should_raise_TimeoutException_when_timeout_is_reached()
        {
            PID pid = SpawnActorFromFunc(EmptyReceive);

            var timeoutEx = await Assert.ThrowsAsync<TimeoutException>(() =>
            {
                return RootContext.Empty.RequestAsync<object>(pid, "", TimeSpan.FromMilliseconds(20));
            });
            Assert.Equal("Request didn't receive any Response within the expected time.", timeoutEx.Message);
        }

        [Fact]
        public async Task RequestActorAsync_should_not_raise_TimeoutException_when_result_is_first()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            });

            var reply = await RootContext.Empty.RequestAsync<object>(pid, "hello", TimeSpan.FromMilliseconds(100));

            Assert.Equal("hey", reply);
        }

        [Fact]
        public async void ActorLifeCycle()
        {
            var messages = new Queue<object>();

            var pid = Actor.Spawn(
                Actor
                    .FromFunc(ctx =>
                    {
                        messages.Enqueue(ctx.Message);
                        return Actor.Done;
                    })
                    .WithMailbox(() => new TestMailbox())
                );

            RootContext.Empty.Send(pid, "hello");
            
            await pid.StopAsync();

            Assert.Equal(4, messages.Count);
            var msgs = messages.ToArray();
            Assert.IsType<Started>(msgs[0]);
            Assert.IsType<string>(msgs[1]);
            Assert.IsType<Stopping>(msgs[2]);
            Assert.IsType<Stopped>(msgs[3]);
        }

        public static PID SpawnForwarderFromFunc(Receive forwarder) => Actor.Spawn(Actor.FromFunc(forwarder));

        [Fact]
        public async Task ForwardActorAsync()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            });

            PID forwarder = SpawnForwarderFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Forward(pid);
                }
                return Actor.Done;
            });

            var reply = await RootContext.Empty.RequestAsync<object>(forwarder, "hello");

            Assert.Equal("hey", reply);
        }
    }
}
