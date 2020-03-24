//----------------------------------------------------------------------------------------------
// <copyright file="StickersController.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace StickersTemplate.Configuration.Controllers
{
    using System;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web.Mvc;
    using ImageResizer;
    using Microsoft.Azure;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using StickersTemplate.Configuration.Models;
    using StickersTemplate.Configuration.Providers;
    using StickersTemplate.Configuration.Providers.Serialization;

    /// <summary>
    /// Stickers Controller
    /// </summary>
    [Authorize]
    public class StickersController : Controller
    {
        private readonly IStickerStore stickerStore;
        private readonly IBlobStore blobStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="StickersController"/> class.
        /// </summary>
        public StickersController()
        {
            var connectionString = CloudConfigurationManager.GetSetting("StorageConnectionString");
            this.stickerStore = new StickerStore(connectionString, CloudConfigurationManager.GetSetting("StickersTableName"));
            this.blobStore = new BlobStore(connectionString, CloudConfigurationManager.GetSetting("StickersBlobContainerName"));
        }

        /// <summary>
        /// GET: Stickers
        /// </summary>
        /// <returns>Task Action Result</returns>
        public async Task<ActionResult> Index()
        {
            var stickers = await this.stickerStore.GetStickersAsync();
            var activeStickers = stickers.Where(s => s.State == StickerState.Active);
            return this.View(activeStickers.Select(s => new StickerViewModel(s)));
        }

        /// <summary>
        /// GET: Stickers/Create
        /// </summary>
        /// <returns>Action Result</returns>
        public ActionResult Create()
        {
            return this.View();
        }

        /// <summary>
        /// POST: Stickers/Create
        /// To protect from overposting attacks, please enable the specific properties you want to bind to, for
        /// more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        /// </summary>
        /// <param name="stickerViewModel">Sticker View Model</param>
        /// <returns>Task Action Result</returns>
        /// [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> Create([Bind(Include = "File,Name,Keywords")] StickerViewModel stickerViewModel)
        {
            if (this.ModelState.IsValid)
            {
                var id = Guid.NewGuid().ToString("D");
                var uri = await this.blobStore.UploadBlobAsync(id, stickerViewModel.File.InputStream);

                await this.stickerStore.CreateStickerAsync(new Sticker
                {
                    Id = id,
                    ImageUri = uri,
                    Name = stickerViewModel.Name,
                    Keywords = stickerViewModel?.GetKeywordsList(),
                    State = StickerState.Active,
                });

                return this.RedirectToAction("Index");
            }

            return this.View(stickerViewModel);
        }

        /// <summary>
        /// GET: Stickers/Edit/5
        /// </summary>
        /// <param name="id">Id</param>
        /// <returns>Task Action Result</returns>
        public async Task<ActionResult> Edit(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Sticker sticker = await this.stickerStore.GetStickerAsync(id);
            if (sticker == null)
            {
                return this.HttpNotFound();
            }

            return this.View(new StickerViewModel(sticker));
        }

        /// <summary>
        /// POST: Stickers/Edit/5
        /// To protect from overposting attacks, please enable the specific properties you want to bind to, for
        /// more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        /// </summary>
        /// <param name="stickerViewModel">Sticker View Model</param>
        /// <returns>Task Action Result</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "Id,Name,Keywords")] StickerViewModel stickerViewModel)
        {
            if (this.ModelState.IsValid)
            {
                Sticker sticker = await this.stickerStore.GetStickerAsync(stickerViewModel.Id);

                sticker.Name = stickerViewModel.Name;
                sticker.Keywords = stickerViewModel?.GetKeywordsList();
                await this.stickerStore.UpdateStickerAsync(sticker);

                return this.RedirectToAction("Index");
            }

            return this.View(stickerViewModel);
        }

        /// <summary>
        /// GET: Stickers/Delete/5
        /// </summary>
        /// <param name="id">Id</param>
        /// <returns>Task Action Result</returns>
        public async Task<ActionResult> Delete(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Sticker sticker = await this.stickerStore.GetStickerAsync(id);
            if (sticker == null)
            {
                return this.HttpNotFound();
            }

            return this.View(new StickerViewModel(sticker));
        }

        /// <summary>
        /// POST: Stickers/Delete/5
        /// </summary>
        /// <param name="id">Id</param>
        /// <returns>Task Action Result</returns>
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Sticker sticker = await this.stickerStore.GetStickerAsync(id);
            if (sticker == null)
            {
                return this.HttpNotFound();
            }

            sticker.State = StickerState.SoftDeleted;
            await this.stickerStore.UpdateStickerAsync(sticker);

            return this.RedirectToAction("Index");
        }

        /// <summary>
        /// POST: Stickers/DeletePermanently/5
        /// </summary>
        /// <param name="id">Id</param>
        /// <returns>Task Action Result</returns>
        [HttpPost]
        [ActionName("DeletePermanently")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeletePermanentlyConfirmed(string id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            await this.stickerStore.DeleteStickerAsync(id);

            return this.RedirectToAction("Index");
        }

        /// <summary>
        /// POST: Stickers/Publish
        /// </summary>
        /// <returns>Task Action Result</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Publish()
        {
            string slackEmojiJson = @"{
            'images': [
                {
                    'name': '1000',
                    'keywords': ['1000', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/1000.png'
                }, {
                    'name': '2cents',
                    'keywords': ['2cents', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/2cents.jpg'
                }, {
                    'name': '420',
                    'keywords': ['420', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/420.png'
                }, {
                    'name': 'a-parrot',
                    'keywords': ['a', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/a-parrot.gif'
                }, {
                    'name': 'adam',
                    'keywords': ['adam', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/adam.gif'
                }, {
                    'name': 'airhorn',
                    'keywords': ['airhorn', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/airhorn.png'
                }, {
                    'name': 'anarchy',
                    'keywords': ['anarchy', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/anarchy.jpg'
                }, {
                    'name': 'angrytrump',
                    'keywords': ['angrytrump', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/angrytrump.png'
                }, {
                    'name': 'avocado-toast',
                    'keywords': ['avocado', 'toast', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/avocado-toast.png'
                }, {
                    'name': 'awyeah',
                    'keywords': ['awyeah', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/awyeah.gif'
                }, {
                    'name': 'ayy',
                    'keywords': ['ayy', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/ayy.png'
                }, {
                    'name': 'babyrich',
                    'keywords': ['babyrich', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/babyrich.jpg'
                }, {
                    'name': 'bacon-strips',
                    'keywords': ['bacon', 'strips', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/bacon-strips.png'
                }, {
                    'name': 'batman',
                    'keywords': ['batman', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/batman.gif'
                }, {
                    'name': 'batman-approves',
                    'keywords': ['batman', 'approves', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/batman-approves.png'
                }, {
                    'name': 'blondeparrot',
                    'keywords': ['blondeparrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/blondeparrot.gif'
                }, {
                    'name': 'bohn',
                    'keywords': ['bohn', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/bohn.png'
                }, {
                    'name': 'bored-parrot',
                    'keywords': ['bored', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/bored-parrot.gif'
                }, {
                    'name': 'botus',
                    'keywords': ['botus', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/botus.png'
                }, {
                    'name': 'bunz',
                    'keywords': ['bunz', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/bunz.png'
                }, {
                    'name': 'businesscat',
                    'keywords': ['businesscat', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/businesscat.jpg'
                }, {
                    'name': 'catparty',
                    'keywords': ['catparty', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/catparty.gif'
                }, {
                    'name': 'caveman',
                    'keywords': ['caveman', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/caveman.jpg'
                }, {
                    'name': 'chainsaw-drone',
                    'keywords': ['chainsaw', 'drone', 'chainsaw-drone', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/chainsaw-drone.gif'
                }, {
                    'name': 'champcheers',
                    'keywords': ['champcheers', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/champcheers.gif'
                }, {
                    'name': 'chancleta',
                    'keywords': ['chancleta', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/chancleta.jpg'
                }, {
                    'name': 'charles',
                    'keywords': ['charles', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/charles.jpg'
                }, {
                    'name': 'charmander',
                    'keywords': ['charmander', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/charmander.gif'
                }, {
                    'name': 'cheers',
                    'keywords': ['cheers', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/cheers.png'
                }, {
                    'name': 'client-email',
                    'keywords': ['client', 'email', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/client-email.png'
                }, {
                    'name': 'comic-sans',
                    'keywords': ['comic', 'sans', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/comic-sans.png'
                }, {
                    'name': 'communism',
                    'keywords': ['communism', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/communism.jpg'
                }, {
                    'name': 'compton',
                    'keywords': ['compton', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/compton.jpg'
                }, {
                    'name': 'conga-parrot-down',
                    'keywords': ['conga', 'parrot', 'down', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/conga-parrot-down.gif'
                }, {
                    'name': 'congaparrot',
                    'keywords': ['congaparrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/congaparrot.gif'
                }, {
                    'name': 'connor',
                    'keywords': ['connor', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/connor.png'
                }, {
                    'name': 'covfefe',
                    'keywords': ['covfefe', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/covfefe.gif'
                }, {
                    'name': 'crossed',
                    'keywords': ['crossed', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/crossed.png'
                }, {
                    'name': 'dancingpusheen',
                    'keywords': ['dancingpusheen', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/dancingpusheen.gif'
                }, {
                    'name': 'dealwithitparrot',
                    'keywords': ['dealwithitparrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/dealwithitparrot.gif'
                }, {
                    'name': 'devil_parrot',
                    'keywords': ['devil', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/devil_parrot.gif'
                }, {
                    'name': 'do-it-live',
                    'keywords': ['do', 'it', 'live', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/do-it-live.png'
                }, {
                    'name': 'do-it-palpatine',
                    'keywords': ['do', 'it', 'palpatine', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/do-it-palpatine.gif'
                }, {
                    'name': 'do-it-shia',
                    'keywords': ['do', 'it', 'shia', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/do-it-shia.gif'
                }, {
                    'name': 'doge',
                    'keywords': ['doge', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/doge.png'
                }, {
                    'name': 'dontannoyme',
                    'keywords': ['dontannoyme', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/dontannoyme.png'
                }, {
                    'name': 'dontmesswithme',
                    'keywords': ['dontmesswithme', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/dontmesswithme.png'
                }, {
                    'name': 'doom',
                    'keywords': ['doom', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/doom.png'
                }, {
                    'name': 'dp',
                    'keywords': ['dp', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/dp.png'
                }, {
                    'name': 'drone',
                    'keywords': ['drone', 'chainsaw-drone', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/drone.gif'
                }, {
                    'name': 'dumpster-fire',
                    'keywords': ['dumpster', 'fire', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/dumpster-fire.gif'
                }, {
                    'name': 'eeeee',
                    'keywords': ['eeeee', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/eeeee.gif'
                }, {
                    'name': 'eric',
                    'keywords': ['eric', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/eric.png'
                }, {
                    'name': 'facepalm',
                    'keywords': ['facepalm', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/facepalm.jpg'
                }, {
                    'name': 'fakenews',
                    'keywords': ['fakenews', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/fakenews.gif'
                }, {
                    'name': 'fan',
                    'keywords': ['fan', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/fan.gif'
                }, {
                    'name': 'fast-parrot',
                    'keywords': ['fast', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/fast-parrot.gif'
                }, {
                    'name': 'fidget-spinner',
                    'keywords': ['fidget', 'spinner', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/fidget-spinner.gif'
                }, {
                    'name': 'fiesta-parrot',
                    'keywords': ['fiesta', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/fiesta-parrot.gif'
                }, {
                    'name': 'finger-wag',
                    'keywords': ['finger', 'wag', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/finger-wag.gif'
                }, {
                    'name': 'fiwi',
                    'keywords': ['fiwi', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/fiwi.gif'
                }, {
                    'name': 'golf-boi',
                    'keywords': ['golf', 'boi', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/golf-boi.png'
                }, {
                    'name': 'grande-coffee',
                    'keywords': ['grande', 'coffee', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/grande-coffee.png'
                }, {
                    'name': 'guyparrot',
                    'keywords': ['guyparrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/guyparrot.gif'
                }, {
                    'name': 'have-some-duck-tape',
                    'keywords': ['have', 'some', 'duck', 'tape', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/have-some-duck-tape.png'
                }, {
                    'name': 'holdthis',
                    'keywords': ['holdthis', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/holdthis.png'
                }, {
                    'name': 'homer-back-away',
                    'keywords': ['homer', 'back', 'away', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/homer-back-away.gif'
                }, {
                    'name': 'hpparrot',
                    'keywords': ['hpparrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/hpparrot.gif'
                }, {
                    'name': 'ice-cream-parrot',
                    'keywords': ['ice', 'cream', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/ice-cream-parrot.gif'
                }, {
                    'name': 'ideas',
                    'keywords': ['ideas', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/ideas.png'
                }, {
                    'name': 'im',
                    'keywords': ['im', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/im.png'
                }, {
                    'name': 'imsmiley',
                    'keywords': ['imsmiley', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/imsmiley.png'
                }, {
                    'name': 'jordan',
                    'keywords': ['jordan', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/jordan.png'
                }, {
                    'name': 'keel',
                    'keywords': ['keel', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/keel.jpg'
                }, {
                    'name': 'keel1',
                    'keywords': ['keel1', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/keel1.png'
                }, {
                    'name': 'keel2',
                    'keywords': ['keel2', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/keel2.png'
                }, {
                    'name': 'kermit-flail',
                    'keywords': ['kermit', 'flail', 'kermit-flail', 'muppet', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/kermit-flail.gif'
                }, {
                    'name': 'klafferman',
                    'keywords': ['klafferman', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/klafferman.png'
                }, {
                    'name': 'krogers',
                    'keywords': ['krogers', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/krogers.png'
                }, {
                    'name': 'leftshark',
                    'keywords': ['leftshark', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/leftshark.gif'
                }, {
                    'name': 'loading',
                    'keywords': ['loading', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/loading.gif'
                }, {
                    'name': 'lorp',
                    'keywords': ['lorp', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/lorp.png'
                }, {
                    'name': 'love-parrot',
                    'keywords': ['love', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/love-parrot.gif'
                }, {
                    'name': 'magahat',
                    'keywords': ['magahat', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/magahat.jpg'
                }, {
                    'name': 'make-it-pop',
                    'keywords': ['make', 'it', 'pop', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/make-it-pop.png'
                }, {
                    'name': 'make-logo-bigger',
                    'keywords': ['make', 'logo', 'bigger', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/make-logo-bigger.png'
                }, {
                    'name': 'matt',
                    'keywords': ['matt', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/matt.png'
                }, {
                    'name': 'mattangry',
                    'keywords': ['mattangry', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/mattangry.png'
                }, {
                    'name': 'mbs',
                    'keywords': ['mbs', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/mbs.gif'
                }, {
                    'name': 'megsdad',
                    'keywords': ['megsdad', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/megsdad.png'
                }, {
                    'name': 'muppet',
                    'keywords': ['muppet', 'kermit-flail', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/muppet.gif'
                }, {
                    'name': 'neat',
                    'keywords': ['neat', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/neat.gif'
                }, {
                    'name': 'netflix',
                    'keywords': ['netflix', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/netflix.png'
                }, {
                    'name': 'no-step-on-snek',
                    'keywords': ['no', 'step', 'on', 'snek', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/no-step-on-snek.jpg'
                }, {
                    'name': 'not-100',
                    'keywords': ['not', '100', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/not-100.png'
                }, {
                    'name': 'nyancat',
                    'keywords': ['nyancat', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/nyancat.gif'
                }, {
                    'name': 'paid',
                    'keywords': ['paid', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/paid.png'
                }, {
                    'name': 'paid-1',
                    'keywords': ['paid', '1', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/paid-1.png'
                }, {
                    'name': 'paid-2',
                    'keywords': ['paid', '2', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/paid-2.png'
                }, {
                    'name': 'panic',
                    'keywords': ['panic', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/panic.jpg'
                }, {
                    'name': 'parrotdad',
                    'keywords': ['parrotdad', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/parrotdad.gif'
                }, {
                    'name': 'parrotliftoff',
                    'keywords': ['parrotliftoff', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/parrotliftoff.gif'
                }, {
                    'name': 'party-dumpster-fire',
                    'keywords': ['party', 'dumpster', 'fire', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/party-dumpster-fire.gif'
                }, {
                    'name': 'party-parrot',
                    'keywords': ['party', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/party-parrot.gif'
                }, {
                    'name': 'party-weed',
                    'keywords': ['party', 'weed', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/party-weed.gif'
                }, {
                    'name': 'peace',
                    'keywords': ['peace', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/peace.jpg'
                }, {
                    'name': 'peak',
                    'keywords': ['peak', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/peak.png'
                }, {
                    'name': 'pinwheel',
                    'keywords': ['pinwheel', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/pinwheel.gif'
                }, {
                    'name': 'pizzaspin',
                    'keywords': ['pizzaspin', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/pizzaspin.gif'
                }, {
                    'name': 'plane',
                    'keywords': ['plane', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/plane.gif'
                }, {
                    'name': 'please',
                    'keywords': ['please', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/please.png'
                }, {
                    'name': 'pointer',
                    'keywords': ['pointer', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/pointer.png'
                }, {
                    'name': 'police',
                    'keywords': ['police', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/police.gif'
                }, {
                    'name': 'pool',
                    'keywords': ['pool', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/pool.png'
                }, {
                    'name': 'pug',
                    'keywords': ['pug', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/pug.png'
                }, {
                    'name': 'red-tape',
                    'keywords': ['red', 'tape', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/red-tape.jpg'
                }, {
                    'name': 'reverseconga-parrot',
                    'keywords': ['reverseconga', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/reverseconga-parrot.gif'
                }, {
                    'name': 'rich',
                    'keywords': ['rich', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/rich.png'
                }, {
                    'name': 'richangry',
                    'keywords': ['richangry', 'richispissed', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/richangry.gif'
                }, {
                    'name': 'richard',
                    'keywords': ['richard', 'richard-petty', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/richard.png'
                }, {
                    'name': 'richard-petty',
                    'keywords': ['richard', 'petty', 'richard-petty', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/richard-petty.png'
                }, {
                    'name': 'richispissed',
                    'keywords': ['richispissed', 'richangry', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/richispissed.gif'
                }, {
                    'name': 'rivers',
                    'keywords': ['rivers', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/rivers.png'
                }, {
                    'name': 'roll',
                    'keywords': ['roll', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/roll.png'
                }, {
                    'name': 'sad-parrot',
                    'keywords': ['sad', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/sad-parrot.gif'
                }, {
                    'name': 'sadegg',
                    'keywords': ['sadegg', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/sadegg.png'
                }, {
                    'name': 'salt',
                    'keywords': ['salt', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/salt.png'
                }, {
                    'name': 'sheldon',
                    'keywords': ['sheldon', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/sheldon.jpg'
                }, {
                    'name': 'shruggs',
                    'keywords': ['shruggs', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/shruggs.png'
                }, {
                    'name': 'shuffle-parrot',
                    'keywords': ['shuffle', 'parrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/shuffle-parrot.gif'
                }, {
                    'name': 'sillypig',
                    'keywords': ['sillypig', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/sillypig.gif'
                }, {
                    'name': 'sketch',
                    'keywords': ['sketch', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/sketch.jpg'
                }, {
                    'name': 'spoopy',
                    'keywords': ['spoopy', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/spoopy.png'
                }, {
                    'name': 'stay-fresh',
                    'keywords': ['stay', 'fresh', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/stay-fresh.png'
                }, {
                    'name': 'stayfresh',
                    'keywords': ['stayfresh', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/stayfresh.png'
                }, {
                    'name': 'suitup',
                    'keywords': ['suitup', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/suitup.gif'
                }, {
                    'name': 'theartistprince',
                    'keywords': ['theartistprince', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/theartistprince.jpg'
                }, {
                    'name': 'thisisfine',
                    'keywords': ['thisisfine', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/thisisfine.png'
                }, {
                    'name': 'thisisfinefire',
                    'keywords': ['thisisfinefire', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/thisisfinefire.gif'
                }, {
                    'name': 'totes',
                    'keywords': ['totes', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/totes.gif'
                }, {
                    'name': 'truth',
                    'keywords': ['truth', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/truth.png'
                }, {
                    'name': 'ukiddingme',
                    'keywords': ['ukiddingme', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/ukiddingme.png'
                }, {
                    'name': 'wat',
                    'keywords': ['wat', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/wat.jpg'
                }, {
                    'name': 'wendyparrot',
                    'keywords': ['wendyparrot', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/wendyparrot.gif'
                }, {
                    'name': 'whatintarnation',
                    'keywords': ['whatintarnation', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/whatintarnation.jpg'
                }, {
                    'name': 'whoa',
                    'keywords': ['whoa', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/whoa.png'
                }, {
                    'name': 'whysoserious',
                    'keywords': ['whysoserious', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/whysoserious.gif'
                }, {
                    'name': 'william',
                    'keywords': ['william', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/william.jpg'
                }, {
                    'name': 'wordpress',
                    'keywords': ['wordpress', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/wordpress.png'
                }, {
                    'name': 'wotintarnation-2',
                    'keywords': ['wotintarnation', '2', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/wotintarnation-2.gif'
                }, {
                    'name': 'xzibit',
                    'keywords': ['xzibit', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/xzibit.png'
                }, {
                    'name': 'yike',
                    'keywords': ['yike', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/yike.png'
                }, {
                    'name': 'zombie_hand',
                    'keywords': ['zombie', 'hand', 'slack'],
                    'imageUri': 'https://elcti4uwcvopi.blob.core.windows.net/stickers/slack/zombie_hand.png'
                }
            ]}";

            var stickers = await this.stickerStore.GetStickersAsync();
            var activeStickers = stickers.Where(s => s.State == StickerState.Active);

            var stickerCollection = activeStickers
                .Select(s => new StickerDTO
                {
                    ImageUri = s.ImageUri.AbsoluteUri,
                    Name = s.Name,
                    Keywords = s.Keywords.ToArray(),
                }).toList()


            List hackishlyImportedSlackEmojis = JsonConvert.DeserializeObject<StickerConfigDTO>(slackEmojiJson);

            foreach (var emoji in hackishlyImportedSlackEmojis.Images)
            {
                stickerCollection.Append(emoji);
            }

            var configuration = new StickerConfigDTO
            {
                Images = stickerCollection.ToArray()
            };

            var configurationJson = JsonConvert.SerializeObject(configuration, Formatting.None);
            await this.blobStore.UploadBlobAsync("stickers.json", new MemoryStream(Encoding.UTF8.GetBytes(configurationJson)));

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }
    }
}